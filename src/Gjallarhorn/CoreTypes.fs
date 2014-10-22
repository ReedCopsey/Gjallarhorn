namespace Gjallarhorn

open System
open System.Collections.Generic

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

    member __.Value with get() = mapping(valueProvider.Value)

    interface IView<'b> with
        member __.Value with get() = mapping(valueProvider.Value)

    interface IDependent with
        member this.RequestRefresh _ =
            SignalManager.Signal(this)


type internal WeakView<'a,'b>(valueProvider : IView<'a>, mapping : 'a -> 'b) as self =
    let mutable v = mapping(valueProvider.Value)

    // Only store a weak reference to our provider
    let handle = WeakReference(valueProvider)

    do
        SignalManager.AddDependency valueProvider self

    member __.Value with get() = v

    interface IView<'b> with
        member __.Value with get() = v

    interface IDependent with
        member this.RequestRefresh _ =
            let provider = handle.Target
            match provider with
            | null -> ()
            | some -> 
                let valueProvider = some :?> IView<'a>
                v <- mapping(valueProvider.Value)
                SignalManager.Signal(this)