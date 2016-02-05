namespace Gjallarhorn

open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System
open System.Collections.Generic

/// Type which allows tracking of multiple disposables at once
type CompositeDisposable() =
    let disposables = ResizeArray<_>()

    override this.Finalize() =
        this.Dispose()
        GC.SuppressFinalize this

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
    let getValue (provider : ISignal<_> option) typeNameFun =
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
        
    let dispose (provider : #ISignal<'a> option) disposeProviderOnDispose (self : IDependent) =
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

    override this.Finalize() =
        this.Dependencies.RemoveAll this        

    interface IObservable<'a> with
        member this.Subscribe obs = this.Dependencies.Subscribe(obs,this)
    interface ITracksDependents with
        member this.Track dep = this.Dependencies.Add (dep,this)
        member this.Untrack dep = this.Dependencies.Remove (dep,this)
    interface IDependent with
        member __.RequestRefresh _ = ()
        member this.HasDependencies with get() = this.Dependencies.HasDependencies
    interface ISignal<'a> with
        member __.Value with get() = v

    interface IMutatable<'a> with
        member this.Value with get() = v and set(v) = this.Value <- v
        
type internal MappingSignal<'a,'b>(valueProvider : ISignal<'a>, mapping : 'a -> 'b, disposeProviderOnDispose : bool) as self =
    let dependencies = Dependencies.create [| valueProvider |] self
    let mutable lastValue = mapping valueProvider.Value
    let mutable valueProvider = Some(valueProvider)

    abstract member Signal : unit -> unit
    default this.Signal() = dependencies.Signal this |> ignore

    member private this.UpdateAndGetValue () =
        let value () = 
            DisposeHelpers.getValue valueProvider (fun _ -> self.GetType().FullName)
            |> mapping
        let value = value()
        if not <| EqualityComparer<_>.Default.Equals(lastValue, value) then
            lastValue <- value
            this.Signal()
        lastValue

    override this.Finalize() =
        (this :> IDisposable).Dispose()        

    interface IObservable<'b> with
        member this.Subscribe obs = dependencies.Subscribe (obs,this)

    interface ITracksDependents with
        member this.Track dep = dependencies.Add (dep,this)
        member this.Untrack dep = dependencies.Remove (dep,this)

    interface ISignal<'b> with
        member this.Value with get() = this.UpdateAndGetValue ()

    interface IDependent with
        member this.RequestRefresh _ =             
            this.UpdateAndGetValue ()
            |> ignore
        member __.HasDependencies with get() = dependencies.HasDependencies

    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider disposeProviderOnDispose this
            valueProvider <- None
            dependencies.RemoveAll this
            GC.SuppressFinalize this            

type internal ObserveOnSignal<'a>(valueProvider : ISignal<'a>, ctx : System.Threading.SynchronizationContext) =
    inherit MappingSignal<'a,'a>(valueProvider, id, false)

    member private __.SignalBase() = base.Signal()

    override this.Signal() =
        ctx.Post (System.Threading.SendOrPostCallback(fun _ -> this.SignalBase()), null)

type internal Mapping2Signal<'a,'b,'c>(valueProvider1 : ISignal<'a>, valueProvider2 : ISignal<'b>, mapping : 'a -> 'b -> 'c) as self =
    let dependencies = Dependencies.create [| valueProvider1 ; valueProvider2 |] self

    let mutable lastValue = mapping valueProvider1.Value valueProvider2.Value
    let mutable valueProvider1 = Some(valueProvider1)
    let mutable valueProvider2 = Some(valueProvider2)

    member private this.Signal() = dependencies.Signal this |> ignore

    member private this.UpdateAndGetValue () =
        let value () = 
            let v1 = DisposeHelpers.getValue valueProvider1 (fun _ -> this.GetType().FullName)
            let v2 = DisposeHelpers.getValue valueProvider2 (fun _ -> this.GetType().FullName)
            mapping v1 v2
        let value = value()
        if not <| EqualityComparer<_>.Default.Equals(lastValue, value) then
            lastValue <- value
            this.Signal()
        lastValue

    override this.Finalize() =
        (this :> IDisposable).Dispose()        

    interface IObservable<'c> with
        member this.Subscribe obs = dependencies.Subscribe (obs,this)

    interface ITracksDependents with
        member this.Track dep = dependencies.Add (dep,this)
        member this.Untrack dep = dependencies.Remove (dep,this)

    interface ISignal<'c> with
        member this.Value with get() = this.UpdateAndGetValue()

    interface IDependent with
        member this.RequestRefresh _ =
            this.UpdateAndGetValue()
            |> ignore
        member __.HasDependencies with get() = dependencies.HasDependencies

    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider1 false this
            DisposeHelpers.dispose valueProvider2 false this
            valueProvider1 <- None
            valueProvider2 <- None
            dependencies.RemoveAll this
            GC.SuppressFinalize this

type internal CombineSignal<'a>(valueProvider1 : ISignal<'a>, valueProvider2 : ISignal<'a>) as self =
    let dependencies = Dependencies.create [| valueProvider1 ; valueProvider2 |] self

    let mutable lastValue = valueProvider2.Value
    let mutable valueProvider1 = Some(valueProvider1)
    let mutable valueProvider2 = Some(valueProvider2)

    member private this.Signal() = dependencies.Signal this |> ignore

    member private this.UpdateAndGetValue (updated : obj) =
        let value () = 
            if obj.ReferenceEquals(updated, valueProvider1.Value) then
                DisposeHelpers.getValue valueProvider1 (fun _ -> this.GetType().FullName)
            else
                DisposeHelpers.getValue valueProvider2 (fun _ -> this.GetType().FullName)            
        if (valueProvider1.IsSome) then
            let value = value()
            if not <| EqualityComparer<_>.Default.Equals(lastValue, value) then
                lastValue <- value
                this.Signal()
        lastValue

    override this.Finalize() =
        (this :> IDisposable).Dispose()        

    interface IObservable<'a> with
        member this.Subscribe obs = dependencies.Subscribe (obs,this)

    interface ITracksDependents with
        member this.Track dep = dependencies.Add (dep,this)
        member this.Untrack dep = dependencies.Remove (dep,this)

    interface ISignal<'a> with
        member this.Value with get() = this.UpdateAndGetValue()

    interface IDependent with
        member this.RequestRefresh updated =
            this.UpdateAndGetValue updated
            |> ignore
        member __.HasDependencies with get() = dependencies.HasDependencies

    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider1 false this
            DisposeHelpers.dispose valueProvider2 false this
            valueProvider1 <- None
            valueProvider2 <- None
            dependencies.RemoveAll this
            GC.SuppressFinalize this

type internal IfSignal<'a>(valueProvider : ISignal<'a>, initialValue, conditionProvider : ISignal<bool>) as self =
    let dependencies = Dependencies.create [| valueProvider ; conditionProvider |] self

    let mutable lastValue = 
        if conditionProvider.Value then
            valueProvider.Value
        else
            initialValue

    let mutable valueProvider = Some(valueProvider)
    let mutable conditionProvider = Some(conditionProvider)

    member private this.Signal() = dependencies.Signal this |> ignore

    member private this.UpdateAndGetValue (updated : obj) =
        let value () = 
            let condition = DisposeHelpers.getValue conditionProvider (fun _ -> this.GetType().FullName)            
            if condition then
                DisposeHelpers.getValue valueProvider (fun _ -> this.GetType().FullName)
            else
                lastValue
                
        let value = value()
        if not <| EqualityComparer<_>.Default.Equals(lastValue, value) then
            lastValue <- value
            this.Signal()
        lastValue

    override this.Finalize() =
        (this :> IDisposable).Dispose()        

    interface IObservable<'a> with
        member this.Subscribe obs = dependencies.Subscribe (obs,this)

    interface ITracksDependents with
        member this.Track dep = dependencies.Add (dep,this)
        member this.Untrack dep = dependencies.Remove (dep,this)

    interface ISignal<'a> with
        member this.Value with get() = this.UpdateAndGetValue()

    interface IDependent with
        member this.RequestRefresh updated =
            this.UpdateAndGetValue updated
            |> ignore
        member __.HasDependencies with get() = dependencies.HasDependencies

    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider false this
            DisposeHelpers.dispose conditionProvider false this
            valueProvider <- None
            conditionProvider <- None
            dependencies.RemoveAll this
            GC.SuppressFinalize this

type internal FilteredSignal<'a> (valueProvider : ISignal<'a>, initialValue : 'a, filter : 'a -> bool, disposeProviderOnDispose : bool) as self =
    let dependencies = Dependencies.create [| valueProvider |] self

    let mutable v = 
        if filter(valueProvider.Value) then
            valueProvider.Value
        else
            initialValue

    let mutable valueProvider = Some(valueProvider)    

    member private this.Signal() = dependencies.Signal this |> ignore

    member private this.UpdateAndSetValue forceSignal =
        let updated =
            match valueProvider with
            | None -> false
            | Some provider ->
                let value = provider.Value                
                if (filter(value)) then
                    if not <| EqualityComparer<'a>.Default.Equals(v, value) then
                        v <- value
                        true
                    else
                        false
                else
                    false
        if updated || forceSignal then
            this.Signal()        


    override this.Finalize() =
        (this :> IDisposable).Dispose()        

    interface IObservable<'a> with
        member this.Subscribe obs = dependencies.Subscribe (obs,this)

    interface ITracksDependents with
        member this.Track dep = dependencies.Add (dep,this)
        member this.Untrack dep = dependencies.Remove (dep,this)

    interface ISignal<'a> with
        member this.Value 
            with get() = 
                this.UpdateAndSetValue false
                v

    interface IDependent with
        member this.RequestRefresh _ = this.UpdateAndSetValue true
            
        member __.HasDependencies with get() = dependencies.HasDependencies
                
    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider disposeProviderOnDispose this
            valueProvider <- None
            dependencies.RemoveAll this
            GC.SuppressFinalize this
        
type internal ChooseSignal<'a,'b>(valueProvider : ISignal<'a>, initialValue : 'b, filter : 'a -> 'b option) as self =
    let dependencies = Dependencies.create [| valueProvider |] self

    let mutable v = 
        match filter(valueProvider.Value) with
        | Some v -> v
        | None -> initialValue

    let mutable valueProvider = Some(valueProvider)
    
    member private this.Signal() = dependencies.Signal this |> ignore

    member private this.UpdateAndSetValue forceSignal =
        let updated =
            match valueProvider with
            | None -> false
            | Some provider ->
                let value = provider.Value                
                match (filter(value)) with
                | Some newValue ->
                    if not <| EqualityComparer<'b>.Default.Equals(v, newValue) then
                        v <- newValue
                        true
                    else
                        false
                | None ->
                    false
        if updated || forceSignal then
            this.Signal()        


    override this.Finalize() =
        (this :> IDisposable).Dispose()        

    interface IObservable<'b> with
        member this.Subscribe obs = dependencies.Subscribe (obs,this)

    interface ITracksDependents with
        member this.Track dep = dependencies.Add (dep,this)
        member this.Untrack dep = dependencies.Remove (dep,this)

    interface ISignal<'b> with
        member this.Value 
            with get() = 
                this.UpdateAndSetValue false
                v

    interface IDependent with
        member this.RequestRefresh _ = this.UpdateAndSetValue true
        member __.HasDependencies with get() = dependencies.HasDependencies

    interface IDisposable with
        member this.Dispose() =
            DisposeHelpers.dispose valueProvider false this
            valueProvider <- None
            dependencies.RemoveAll this
            GC.SuppressFinalize this            

type internal CachedSignal<'a> (valueProvider : ISignal<'a>) as self =
    let dependencies = Dependencies.create [| valueProvider |] self

    let mutable v = valueProvider.Value

    // Caching acts like a subscription, since it has to update in case the
    // target is GCed
    // Note: Tracking does not hold a strong reference, so disposal is not necessary still
    do 
        valueProvider.Track self

    // Only store a weak reference to our provider
    let handle = WeakReference<_>(valueProvider)

    member private this.Signal() = dependencies.Signal this |> ignore

    member private this.UpdateAndGetValue () =        
        handle
        |> WeakRef.execute (fun provider ->
            let value = provider.Value
            if not <| EqualityComparer<'a>.Default.Equals(v, value) then
                v <- value
                this.Signal())
        |> ignore
        v

    override this.Finalize() =
        (this :> IDisposable).Dispose()        

    interface IObservable<'a> with
        member this.Subscribe obs = dependencies.Subscribe (obs,this)

    interface ITracksDependents with
        member this.Track dep = dependencies.Add (dep,this)
        member this.Untrack dep = dependencies.Remove (dep,this)

    interface ISignal<'a> with
        member this.Value with get() = this.UpdateAndGetValue()

    interface IDependent with
        member this.RequestRefresh _ =
            this.UpdateAndGetValue()
            |> ignore
        member __.HasDependencies with get() = dependencies.HasDependencies

    interface IDisposable with
        member this.Dispose() =
            handle
            |> WeakRef.execute (fun v ->
                v.Untrack this                    
                handle.SetTarget(Unchecked.defaultof<ISignal<'a>>))
            |> ignore
            dependencies.RemoveAll this
            GC.SuppressFinalize this