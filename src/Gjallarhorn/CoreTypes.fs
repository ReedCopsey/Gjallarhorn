namespace Gjallarhorn

open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System
open System.Collections.Generic

/// Type which allows tracking of multiple disposables at once
type CompositeDisposable() =
    let disposables = ResizeArray<_>()

    /// Add a new disposable to this tracker
    member __.Add (disposable : IDisposable) = disposables.Add(disposable)
    /// Remove a disposable from this tracker without disposing of it
    member __.Remove (disposable : IDisposable) = disposables.Remove(disposable)

    /// Dispose all of our tracked disposables and remove them all 
    member __.Dispose() =
        disposables
        |> Seq.iter (fun d -> d.Dispose())
        disposables.Clear()

    interface IDisposable with
        /// Dispose all of our tracked disposables and remove them all 
        member this.Dispose() = this.Dispose()

module internal DisposeHelpers =
    let getValue (provider : IView<_> option) typeNameFun =
        match provider with 
        | Some(v) -> v.Value
        | None -> raise <| ObjectDisposedException(typeNameFun())        

    let setValue (provider : IMutatable<_> option) mapping value typeNameFun =
        match provider with 
        | Some(v) -> v.Value <- mapping(value)
        | None -> raise <| ObjectDisposedException(typeNameFun())        

    let disposeIfDisposable (v : obj) =
        match v with
        | :? IDisposable as d -> 
            d.Dispose()
        | _ -> ()
        
    let dispose (provider : #IView<'a> option) disposeProviderOnDispose (self : IDependent) =
            match provider with
            | None -> ()
            | Some(v) ->
                v.Untrack self
                
                if disposeProviderOnDispose then
                    disposeIfDisposable v

// A lightweight wrapper for a mutable value which provides a mechanism for change notification as needed
type internal Mutable<'a>(value : 'a) =

    let mutable v = value

    // Stores dependencies remotely to not use any space in the object (no memory overhead requirements)
    member private this.Dependencies with get() = Dependencies.createRemote this
    
    member this.Value 
        with get() = v
        and set(value) =
            if not(EqualityComparer<'a>.Default.Equals(v, value)) then            
                v <- value
                this.Dependencies.Signal(this)

    interface IObservable<'a> with
        member this.Subscribe obs = 
            this.Dependencies.Add obs
            { 
                new IDisposable with
                    member __.Dispose() = this.Dependencies.Remove obs
            }
    interface ITracksDependents with
        member this.Track dep = this.Dependencies.Add dep
        member this.Untrack dep = this.Dependencies.Remove dep
    interface IView<'a> with
        member __.Value with get() = v

    interface IMutatable<'a> with
        member this.Value with get() = v and set(v) = this.Value <- v
        
type internal MappingView<'a,'b>(valueProvider : IView<'a>, mapping : 'a -> 'b, disposeProviderOnDispose : bool) as self =
    do
        // TODO: Remove this until needed
        valueProvider.Track self

    let dependencies = Dependencies.create [| valueProvider |]
    let mutable valueProvider = Some(valueProvider)

    let value () = 
        DisposeHelpers.getValue valueProvider (fun _ -> self.GetType().FullName)
        |> mapping

    override this.Finalize() =
        (this :> IDisposable).Dispose()
        GC.SuppressFinalize this        

    abstract member Disposing : unit -> unit
    default __.Disposing() =
        ()

    abstract member Refreshing : unit -> unit
    default __.Refreshing() =
        ()

    interface IObservable<'b> with
        member __.Subscribe obs = 
            dependencies.Add obs
            { 
                new IDisposable with
                    member __.Dispose() = dependencies.Remove obs
            }

    interface ITracksDependents with
        member __.Track dep = dependencies.Add dep
        member __.Untrack dep = dependencies.Remove dep

    interface IDisposableView<'b> with
        member __.Value with get() = value()

    interface IDependent with
        member this.RequestRefresh _ = 
            this.Refreshing()
            dependencies.Signal this |> ignore
        member __.HasDependencies with get() = dependencies.HasDependencies

    interface IDisposable with
        member this.Dispose() =
            this.Disposing()
            DisposeHelpers.dispose valueProvider disposeProviderOnDispose this
            valueProvider <- None
            dependencies.RemoveAll()

type internal Mapping2View<'a,'b,'c>(valueProvider1 : IView<'a>, valueProvider2 : IView<'b>, mapping : 'a -> 'b -> 'c) as self =
    do
        valueProvider1.Track self
        valueProvider2.Track self

    let dependencies = Dependencies.create [| valueProvider1 ; valueProvider2 |]

    let mutable valueProvider1 = Some(valueProvider1)
    let mutable valueProvider2 = Some(valueProvider2)

    let value () = 
        let v1 = DisposeHelpers.getValue valueProvider1 (fun _ -> self.GetType().FullName)
        let v2 = DisposeHelpers.getValue valueProvider2 (fun _ -> self.GetType().FullName)
        mapping v1 v2

    override this.Finalize() =
        (this :> IDisposable).Dispose()
        GC.SuppressFinalize this

    interface IObservable<'c> with
        member __.Subscribe obs = 
            dependencies.Add obs
            { 
                new IDisposable with
                    member __.Dispose() = dependencies.Remove obs
            }

    interface ITracksDependents with
        member __.Track dep = dependencies.Add dep
        member __.Untrack dep = dependencies.Remove dep

    interface IDisposableView<'c> with
        member __.Value with get() = value()

    interface IDependent with
        member this.RequestRefresh _ =
            dependencies.Signal this |> ignore
        member __.HasDependencies with get() = dependencies.HasDependencies

    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider1 false this
            DisposeHelpers.dispose valueProvider2 false this
            valueProvider1 <- None
            valueProvider2 <- None
            dependencies.RemoveAll()

type internal FilteredView<'a> (valueProvider : IView<'a>, filter : 'a -> bool, disposeProviderOnDispose : bool) as self =
    do
        valueProvider.Track self

    let dependencies = Dependencies.create [| valueProvider |]

    let mutable v = valueProvider.Value

    let mutable valueProvider = Some(valueProvider)
    let signal() = dependencies.Signal self |> ignore

    override this.Finalize() =
        (this :> IDisposable).Dispose()
        GC.SuppressFinalize this

    interface IObservable<'a> with
        member __.Subscribe obs = 
            dependencies.Add obs
            { 
                new IDisposable with
                    member __.Dispose() = dependencies.Remove obs
            }

    interface ITracksDependents with
        member __.Track dep = dependencies.Add dep
        member __.Untrack dep = dependencies.Remove dep

    interface IDisposableView<'a> with
        member __.Value with get() = v

    interface IDependent with
        member __.RequestRefresh _ = 
            match valueProvider with
            | None -> ()
            | Some(provider) ->
                let value = provider.Value
                if filter(value) then
                    v <- value
                    signal()
        member __.HasDependencies with get() = dependencies.HasDependencies
                
    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider disposeProviderOnDispose this
            valueProvider <- None
            dependencies.RemoveAll()

type internal CachedView<'a> (valueProvider : IView<'a>) as self =
    do
        valueProvider.Track self

    let dependencies = Dependencies.create [| valueProvider |]

    let mutable v = valueProvider.Value

    // Only store a weak reference to our provider
    let mutable handle = WeakReference<_>(valueProvider)

    let signal() = dependencies.Signal self |> ignore

    override this.Finalize() =
        (this :> IDisposable).Dispose()
        GC.SuppressFinalize this

    interface IObservable<'a> with
        member __.Subscribe obs = 
            dependencies.Add obs
            { 
                new IDisposable with
                    member __.Dispose() = dependencies.Remove obs
            }

    interface ITracksDependents with
        member __.Track dep = dependencies.Add dep
        member __.Untrack dep = dependencies.Remove dep

    interface IDisposableView<'a> with
        member __.Value with get() = v

    interface IDependent with
        member __.RequestRefresh _ =
            if handle <> null then
                match handle.TryGetTarget() with
                | true, provider -> 
                    let value = provider.Value                    
                    v <- value
                    signal()
                | false,_ -> ()
        member __.HasDependencies with get() = dependencies.HasDependencies

    interface IDisposable with
        member this.Dispose() =
            if handle <> null then
                match handle.TryGetTarget() with
                | true, v ->
                    v.Untrack this
                    handle <- null
                | false,_ -> ()
            dependencies.RemoveAll()