namespace Gjallarhorn

open Gjallarhorn.Internal

open System
open System.Collections.Generic


module internal DisposeHelpers =
    let getValue (provider : IView<_> option) typeNameFun =
        match provider with 
        | Some(v) -> v.Value
        | None -> raise <| ObjectDisposedException(typeNameFun())        

    let dispose (provider : IView<'a> option) self =
            match provider with
            | None -> ()
            | Some(v) ->
                v.RemoveDependency self

// A lightweight wrapper for a mutable value which provides a mechanism for change notification as needed
type internal Mutable<'a>(value : 'a) =

    let mutable v = value

    member this.Signal () = SignalManager.Signal(this)
     
    member this.Value 
        with get() = v
        and set(value) =
            if not(EqualityComparer<'a>.Default.Equals(v, value)) then            
                v <- value
                this.Signal()

    interface IView<'a> with
        member __.Value with get() = v
        member this.AddDependency dep =            
            SignalManager.AddDependency this dep                
        member this.RemoveDependency dep =
            SignalManager.RemoveDependency this dep
        member this.Signal () =
            this.Signal()

    interface IMutatable<'a> with
        member this.Value with get() = v and set(v) = this.Value <- v

type internal View<'a,'b>(valueProvider : IView<'a>, mapping : 'a -> 'b) as self =
    do
        valueProvider.AddDependency self

    let mutable valueProvider = Some(valueProvider)

    let value () = 
        DisposeHelpers.getValue valueProvider (fun _ -> self.GetType().FullName)
        |> mapping

    member __.Value with get() = value()

    member this.Signal () = SignalManager.Signal(this)

    interface IDisposableView<'b> with
        member __.Value with get() = value()
        member this.AddDependency dep =
            SignalManager.AddDependency this dep                
        member this.RemoveDependency dep =
            SignalManager.RemoveDependency this dep
        member this.Signal () =
            this.Signal()

    interface IDependent with
        member this.RequestRefresh _ =
            this.Signal()

    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider this
            valueProvider <- None

type internal View2<'a,'b,'c>(valueProvider1 : IView<'a>, valueProvider2 : IView<'b>, mapping : 'a -> 'b -> 'c) as self =
    do
        valueProvider1.AddDependency self
        valueProvider2.AddDependency self

    let mutable valueProvider1 = Some(valueProvider1)
    let mutable valueProvider2 = Some(valueProvider2)

    let value () = 
        let v1 = DisposeHelpers.getValue valueProvider1 (fun _ -> self.GetType().FullName)
        let v2 = DisposeHelpers.getValue valueProvider2 (fun _ -> self.GetType().FullName)
        mapping v1 v2

    member __.Value with get() = value()

    member this.Signal() = SignalManager.Signal(this)

    interface IDisposableView<'c> with
        member __.Value with get() = value()
        member this.AddDependency dep =
            SignalManager.AddDependency this dep                
        member this.RemoveDependency dep =
            SignalManager.RemoveDependency this dep
        member this.Signal () =
            this.Signal()

    interface IDependent with
        member this.RequestRefresh _ =
            this.Signal()

    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider1 this
            DisposeHelpers.dispose valueProvider2 this
            valueProvider1 <- None
            valueProvider2 <- None

type internal ViewCache<'a> (valueProvider : IView<'a>, ?filter : 'a -> bool) as self =
    let mutable v = valueProvider.Value

    // Only store a weak reference to our provider
    let mutable handle = WeakReference(valueProvider)

    let shouldSignal value = 
        match filter with
        | Some(f) -> f(value)
        | None -> true

    do
        // Use SignalManager explicitly here since we're a "cached" view
        SignalManager.AddDependency valueProvider self    

    member this.Signal() = SignalManager.Signal(this)

    interface IDisposableView<'a> with
        member __.Value with get() = v
        member this.AddDependency dep =
            SignalManager.AddDependency this dep                
        member this.RemoveDependency dep =
            SignalManager.RemoveDependency this dep
        member this.Signal () =
            this.Signal()

    interface IDependent with
        member this.RequestRefresh _ =
            if handle <> null then
                match handle.Target with
                | :? IView<'a> as provider -> 
                    let value = provider.Value
                    if shouldSignal(value) then
                        v <- value
                        this.Signal()
                | _ -> ()

    interface IDisposable with
        member this.Dispose() =
            if handle <> null then
                match handle.Target with
                | :? IView<'a> as v ->
                    v.RemoveDependency this
                    handle <- null
                | _ -> ()
