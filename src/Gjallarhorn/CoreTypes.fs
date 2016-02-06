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
        
    let cleanup (provider : #ISignal<'a> option byref) disposeProviderOnDispose (self : #IDependent) =
            match provider with
            | None -> ()
            | Some(v) ->
                v.Untrack self
                
                if disposeProviderOnDispose then
                    disposeIfDisposable v

                provider <- None

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

[<AbstractClass>]       
/// Base class which simplifies implementation of standard signals
type SignalBase<'a>(dependencies) as self =
    let dependencies = Dependencies.create dependencies self

    /// Signals to dependencies that we have updated
    abstract member Signal : unit -> unit
    default this.Signal() = dependencies.Signal this |> ignore
    
    /// Gets the current value
    abstract member Value : 'a with get
    /// Notifies us that we need to refresh our value
    abstract member RequestRefresh : obj -> unit
    /// Called during the disposable process
    abstract member OnDisposing : unit -> unit

    /// Default implementations work off single set of dependenices
    abstract member HasDependencies : bool with get
    default __.HasDependencies with get() = dependencies.HasDependencies

    override this.Finalize() =
        (this :> IDisposable).Dispose()        

    interface ISignal<'a> with
        member this.Value with get() = this.Value

    interface IDependent with
        member this.RequestRefresh obj = this.RequestRefresh obj
        member this.HasDependencies with get() = this.HasDependencies

    interface IObservable<'a> with
        member this.Subscribe obs = dependencies.Subscribe (obs,this)

    interface ITracksDependents with
        member this.Track dep = dependencies.Add (dep,this)
        member this.Untrack dep = dependencies.Remove (dep,this)

    interface IDisposable with
        member this.Dispose () =
            this.OnDisposing ()
            dependencies.RemoveAll this
            GC.SuppressFinalize this

type internal MappingSignal<'a,'b>(valueProvider : ISignal<'a>, mapping : 'a -> 'b, disposeProviderOnDispose : bool) =
    inherit SignalBase<'b>([| valueProvider |])
    
    let mutable lastValue = mapping valueProvider.Value
    let mutable valueProvider = Some(valueProvider)

    member private this.Update () =
        let value = 
            DisposeHelpers.getValue valueProvider (fun _ -> this.GetType().FullName)
            |> mapping
        if not <| EqualityComparer<_>.Default.Equals(lastValue, value) then
            lastValue <- value
            this.Signal()        

    override this.Value 
        with get() = 
            this.Update()
            lastValue

    override this.RequestRefresh _ = this.Update () 

    override this.OnDisposing () =
        this |> DisposeHelpers.cleanup &valueProvider disposeProviderOnDispose 

type internal ObserveOnSignal<'a>(valueProvider : ISignal<'a>, ctx : System.Threading.SynchronizationContext) =
    inherit MappingSignal<'a,'a>(valueProvider, id, false)

    member private __.SignalBase() = base.Signal()
    override this.Signal() = ctx.Post (System.Threading.SendOrPostCallback(fun _ -> this.SignalBase()), null)

type internal Mapping2Signal<'a,'b,'c>(valueProvider1 : ISignal<'a>, valueProvider2 : ISignal<'b>, mapping : 'a -> 'b -> 'c) =
    inherit SignalBase<'c>([| valueProvider1 ; valueProvider2 |])

    let mutable lastValue = mapping valueProvider1.Value valueProvider2.Value
    let mutable valueProvider1 = Some(valueProvider1)
    let mutable valueProvider2 = Some(valueProvider2)

    member private this.Update () =
        let value = 
            let v1 = DisposeHelpers.getValue valueProvider1 (fun _ -> this.GetType().FullName)
            let v2 = DisposeHelpers.getValue valueProvider2 (fun _ -> this.GetType().FullName)
            mapping v1 v2
        if not <| EqualityComparer<_>.Default.Equals(lastValue, value) then
            lastValue <- value
            this.Signal()

    override this.Value
        with get() =
            this.Update()
            lastValue

    override this.RequestRefresh _ = this.Update()

    override this.OnDisposing () =
        this |> DisposeHelpers.cleanup &valueProvider1 false
        this |> DisposeHelpers.cleanup &valueProvider2 false 

type internal CombineSignal<'a>(valueProvider1 : ISignal<'a>, valueProvider2 : ISignal<'a>) =
    inherit SignalBase<'a>([| valueProvider1 ; valueProvider2 |])

    let mutable lastValue = valueProvider2.Value
    let mutable valueProvider1 = Some(valueProvider1)
    let mutable valueProvider2 = Some(valueProvider2)

    member private this.Update (updated : obj) =
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

    override __.Value with get() = lastValue
    override this.RequestRefresh obj = this.Update obj

    override this.OnDisposing () =
        this |> DisposeHelpers.cleanup &valueProvider1 false 
        this |> DisposeHelpers.cleanup &valueProvider2 false 

type internal IfSignal<'a>(valueProvider : ISignal<'a>, initialValue, conditionProvider : ISignal<bool>) =
    inherit SignalBase<'a>([| valueProvider ; conditionProvider |])

    let mutable lastValue = if conditionProvider.Value then valueProvider.Value else initialValue

    let mutable valueProvider = Some(valueProvider)
    let mutable conditionProvider = Some(conditionProvider)

    member private this.Update () =
        let value = 
            let condition = DisposeHelpers.getValue conditionProvider (fun _ -> this.GetType().FullName)            
            if condition then
                DisposeHelpers.getValue valueProvider (fun _ -> this.GetType().FullName)
            else
                lastValue

        if not <| EqualityComparer<_>.Default.Equals(lastValue, value) then
            lastValue <- value
            this.Signal()        

    override this.Value 
        with get() = 
            this.Update()
            lastValue
    override this.RequestRefresh _ = this.Update ()

    override this.OnDisposing () =
        this |> DisposeHelpers.cleanup &valueProvider false 
        this |> DisposeHelpers.cleanup &conditionProvider false

type internal FilteredSignal<'a> (valueProvider : ISignal<'a>, initialValue : 'a, filter : 'a -> bool, disposeProviderOnDispose : bool) =
    inherit SignalBase<'a>([| valueProvider |])

    let mutable lastValue = if filter(valueProvider.Value) then valueProvider.Value else initialValue

    let mutable valueProvider = Some(valueProvider)    

    member private this.Update forceSignal =
        let updated =
            match valueProvider with
            | None -> false
            | Some provider ->
                let value = provider.Value                
                if (filter(value)) then
                    if not <| EqualityComparer<'a>.Default.Equals(lastValue, value) then
                        lastValue <- value
                        true
                    else
                        false
                else
                    false
        if updated || forceSignal then
            this.Signal()        

    override this.Value 
        with get() = 
            this.Update false
            lastValue
    override this.RequestRefresh _ = this.Update true

    override this.OnDisposing () =
        this |> DisposeHelpers.cleanup &valueProvider disposeProviderOnDispose 
                        
type internal ChooseSignal<'a,'b>(valueProvider : ISignal<'a>, initialValue : 'b, filter : 'a -> 'b option) =
    inherit SignalBase<'b>([| valueProvider |])

    let mutable lastValue = 
        match filter(valueProvider.Value) with
        | Some v -> v
        | None -> initialValue

    let mutable valueProvider = Some(valueProvider)
    

    member private this.Update forceSignal =
        let updated =
            match valueProvider with
            | None -> false
            | Some provider ->
                let value = provider.Value                
                match (filter(value)) with
                | Some newValue ->
                    if not <| EqualityComparer<'b>.Default.Equals(lastValue, newValue) then
                        lastValue <- newValue
                        true
                    else
                        false
                | None ->
                    false
        if updated || forceSignal then
            this.Signal()        

    override this.Value 
        with get() = 
            this.Update false
            lastValue

    override this.RequestRefresh _ = this.Update true

    override this.OnDisposing () =
        this |> DisposeHelpers.cleanup &valueProvider false

type internal CachedSignal<'a> (valueProvider : ISignal<'a>) as self =
    inherit SignalBase<'a>([| valueProvider |])

    let mutable lastValue = valueProvider.Value

    // Caching acts like a subscription, since it has to update in case the
    // target is GCed
    // Note: Tracking does not hold a strong reference, so disposal is not necessary still
    do 
        valueProvider.Track self

    // Only store a weak reference to our provider
    let handle = WeakReference<_>(valueProvider)

    member private this.Update () =        
        handle
        |> WeakRef.execute (fun provider ->
            let value = provider.Value
            if not <| EqualityComparer<'a>.Default.Equals(lastValue, value) then
                lastValue <- value
                this.Signal())
        |> ignore

    override this.Value 
        with get() = 
            this.Update ()
            lastValue

    override this.RequestRefresh _ = this.Update ()

    override this.OnDisposing () =
        handle
        |> WeakRef.execute (fun v ->
            v.Untrack this                    
            handle.SetTarget(Unchecked.defaultof<ISignal<'a>>))
        |> ignore
    
/// Type which tracks execution, used for tracked async operations
/// Signal with value of true when idle, false when executing
type IdleTracker(ctx : System.Threading.SynchronizationContext) =
    inherit SignalBase<bool>([| |])

    let handles = ResizeArray<_>()
        
    member private this.AddHandle h =
        lock handles (fun _ ->
            handles.Add h
            this.Signal()            
        )
    member private this.RemoveHandle h =
        lock handles (fun _ ->
            if handles.Remove h then this.Signal()            
        )

    /// Gets an execution handle, which makes this as executing until the handle is disposed.
    /// Mutiple execution handles can be pulled simultaneously
    member this.GetExecutionHandle () =
        let rec handle = 
            { new System.IDisposable with
                member __.Dispose() =
                    this.RemoveHandle handle
            }
        this.AddHandle handle
        handle

    member private __.SignalBase() = base.Signal()
    override this.Signal() = ctx.Post (System.Threading.SendOrPostCallback(fun _ -> this.SignalBase()), null)

    override __.Value with get() = lock handles (fun _ -> handles.Count = 0)
    override __.RequestRefresh _ = ()
    override __.OnDisposing () = ()

type internal AsyncMappingSignal<'a,'b>(valueProvider : ISignal<'a>, initialValue : 'b, tracker: IdleTracker option, mapFn : 'a -> Async<'b>, ?cancellationToken : System.Threading.CancellationToken) =
    inherit SignalBase<'b>([| valueProvider |])

    let mutable lastValue = initialValue

    let mutable valueProvider = Some(valueProvider)    
    let ctx = System.Threading.SynchronizationContext.Current

    member private this.Update () =
        let inputValue = 
            DisposeHelpers.getValue valueProvider (fun _ -> this.GetType().FullName)

        let exec =             
            async {
                use _execHandle = 
                    match tracker with
                    | None ->
                        { new IDisposable with
                            member __.Dispose() = ()
                        }
                    | Some tracker ->
                        tracker.GetExecutionHandle()                
                let! result = mapFn(inputValue)

                if not <| EqualityComparer<_>.Default.Equals(lastValue, result) then    
                    if (ctx <> null) then
                        do! Async.SwitchToContext ctx
                    lastValue <- result
                    this.Signal ()    
            }
        
        Async.Start(exec, defaultArg cancellationToken System.Threading.CancellationToken.None )    

    override __.Value with get() = lastValue

    override this.RequestRefresh _ = this.Update ()

    override this.OnDisposing () =
        this |> DisposeHelpers.cleanup &valueProvider false