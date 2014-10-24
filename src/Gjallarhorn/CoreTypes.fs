namespace Gjallarhorn

open System
open System.Collections.Generic


module internal DisposeHelpers =
    let getValue (provider : IView<_> option) typeNameFun =
        match provider with 
        | Some(v) -> v.Value
        | None -> raise <| ObjectDisposedException(typeNameFun())        

    let dispose provider self =
            match provider with
            | None -> ()
            | Some(v) ->
                SignalManager.RemoveDependency v self

// A lightweight wrapper for a mutable value which provides a mechanism for change notification as needed
type internal Mutable<'a>(value : 'a) =

    let mutable v = value
            
    member this.Value 
        with get() = v
        and set(value) =
            if not(EqualityComparer<'a>.Default.Equals(v, value)) then            
                v <- value
                SignalManager.Signal(this)

    interface IView<'a> with
        member __.Value with get() = v

    interface IMutatable<'a> with
        member this.Value with get() = v and set(v) = this.Value <- v

type internal View<'a,'b>(valueProvider : IView<'a>, mapping : 'a -> 'b) as self =
    do
        SignalManager.AddDependency valueProvider self

    let mutable valueProvider = Some(valueProvider)

    let value () = 
        DisposeHelpers.getValue valueProvider (fun _ -> self.GetType().FullName)
        |> mapping

    member __.Value with get() = value()

    interface IDisposableView<'b> with
        member __.Value with get() = value()

    interface IDependent with
        member this.RequestRefresh _ =
            SignalManager.Signal(this)

    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider this
            valueProvider <- None

type internal View2<'a,'b,'c>(valueProvider1 : IView<'a>, valueProvider2 : IView<'b>, mapping : 'a -> 'b -> 'c) as self =
    do
        SignalManager.AddDependency valueProvider1 self
        SignalManager.AddDependency valueProvider2 self

    let mutable valueProvider1 = Some(valueProvider1)
    let mutable valueProvider2 = Some(valueProvider2)

    let value () = 
        let v1 = DisposeHelpers.getValue valueProvider1 (fun _ -> self.GetType().FullName)
        let v2 = DisposeHelpers.getValue valueProvider2 (fun _ -> self.GetType().FullName)
        mapping v1 v2

    member __.Value with get() = value()

    interface IDisposableView<'c> with
        member __.Value with get() = value()

    interface IDependent with
        member this.RequestRefresh _ =
            SignalManager.Signal(this)

    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider1 this
            DisposeHelpers.dispose valueProvider2 this
            valueProvider1 <- None
            valueProvider2 <- None

type internal ViewCache<'a>(valueProvider : IView<'a>) as self =
    let mutable v = valueProvider.Value

    // Only store a weak reference to our provider
    let mutable handle = WeakReference(valueProvider)

    do
        SignalManager.AddDependency valueProvider self

    member __.Value with get() = v

    interface IDisposableView<'a> with
        member __.Value with get() = v

    interface IDependent with
        member this.RequestRefresh _ =
            if handle <> null then
                match handle.Target with
                | :? IView<'a> as provider -> 
                    v <- provider.Value
                    SignalManager.Signal(this)
                | _ -> ()

    interface IDisposable with
        member this.Dispose() =
            if handle <> null then
                match handle.Target with
                | :? IView<'a> as v ->
                    SignalManager.RemoveDependency v this
                    handle <- null
                | _ -> ()
