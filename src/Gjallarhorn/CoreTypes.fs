namespace Gjallarhorn.Helpers

open Gjallarhorn
open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System
open System.Collections.Generic
open System.Runtime.CompilerServices

[<Extension>]
type internal FSharpFuncExtensions = 
    [<Extension>] 
    static member ToFSharpFunc<'a,'b> (func:System.Func<'a,'b>) = fun a -> func.Invoke(a)
    [<Extension>] 
    static member ToFSharpFunc<'a,'b,'c> (func:System.Func<'a,'b,'c>) = fun a b -> func.Invoke(a,b)
    [<Extension>] 
    static member ToFSharpFunc<'a,'b,'c,'d> (func:System.Func<'a,'b,'c,'d>) = fun a b c -> func.Invoke(a,b,c)
    [<Extension>] 
    static member ToFSharpFunc<'a,'b,'c,'d,'e> (func:System.Func<'a,'b,'c,'d,'e>) = fun a b c d -> func.Invoke(a,b,c,d)
    [<Extension>] 
    static member ToFSharpFunc<'a,'b,'c,'d,'e,'f> (func:System.Func<'a,'b,'c,'d,'e,'f>) = fun a b c d e -> func.Invoke(a,b,c,d,e)
    [<Extension>] 
    static member ToFSharpFunc<'a,'b,'c,'d,'e,'f,'g> (func:System.Func<'a,'b,'c,'d,'e,'f,'g>) = fun a b c d e f -> func.Invoke(a,b,c,d,e,f)
    [<Extension>] 
    static member ToFSharpFunc<'a,'b,'c,'d,'e,'f,'g,'h> (func:System.Func<'a,'b,'c,'d,'e,'f,'g,'h>) = fun a b c d e f g -> func.Invoke(a,b,c,d,e,f,g)
    [<Extension>] 
    static member ToFSharpFunc<'a,'b,'c,'d,'e,'f,'g,'h,'i> (func:System.Func<'a,'b,'c,'d,'e,'f,'g,'h,'i>) = fun a b c d e f g h -> func.Invoke(a,b,c,d,e,f,g,h)
    [<Extension>] 
    static member ToFSharpFunc<'a,'b,'c,'d,'e,'f,'g,'h,'i,'j> (func:System.Func<'a,'b,'c,'d,'e,'f,'g,'h,'i,'j>) = fun a b c d e f g h i -> func.Invoke(a,b,c,d,e,f,g,h,i)
    [<Extension>] 
    static member ToFSharpFunc<'a,'b,'c,'d,'e,'f,'g,'h,'i,'j,'k> (func:System.Func<'a,'b,'c,'d,'e,'f,'g,'h,'i,'j,'k>) = fun a b c d e f g h i j -> func.Invoke(a,b,c,d,e,f,g,h,i,j)

/// A disposable type that manages multiple other disposables, and disposes all of them when disposed
type ICompositeDisposable =
    inherit IDisposable

    /// Add an idisposable to this composite disposable
    abstract member Add : IDisposable -> unit
    /// Remove an idisposable to this composite disposable
    abstract member Remove : IDisposable -> unit

/// Type which allows tracking of multiple disposables at once
type CompositeDisposable() =
    let disposables = ResizeArray<_>()

    override this.Finalize() =
        this.Dispose()
        GC.SuppressFinalize this

    /// Add a new disposable to this tracker
    member __.Add (disposable : IDisposable) = disposables.Add(disposable)
    /// Remove a disposable from this tracker without disposing of it
    member __.Remove (disposable : IDisposable) = disposables.Remove(disposable) |> ignore

    /// Dispose all of our tracked disposables and remove them all 
    member __.Dispose() =
        disposables
        |> Seq.iter (fun d -> d.Dispose())
        disposables.Clear()

    interface ICompositeDisposable with
        member this.Add d = this.Add d
        member this.Remove d = this.Remove d 

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

namespace Gjallarhorn.Internal

open System
open System.Collections.Generic

open Gjallarhorn
open Gjallarhorn.Helpers

/// A lightweight wrapper for a mutable value which provides a mechanism for change notification as needed
type Mutable<'a>(value : 'a) =

    let mutable v = value

    // Stores dependencies remotely to not use any space in the object (no memory overhead requirements)
    member private this.Dependencies with get() = Dependencies.createRemote this
    
    /// Gets and sets the Value contained within this mutable
    member this.Value 
        with get() = v
        and set(value) =
            if not(EqualityComparer<'a>.Default.Equals(v, value)) then            
                v <- value
                this.Dependencies.MarkDirty(this)

    override this.Finalize() =
        this.Dependencies.RemoveAll this        

    interface IObservable<'a> with
        member this.Subscribe obs = this.Dependencies.Subscribe(obs,this)
    interface ITracksDependents with
        member this.Track dep = this.Dependencies.Add (dep,this)
        member this.Untrack dep = this.Dependencies.Remove (dep,this)
    interface IDependent with
        member __.UpdateDirtyFlag _ = ()
        member this.HasDependencies with get() = this.Dependencies.HasDependencies
    interface ISignal<'a> with
        member __.Value with get() = v

    interface IMutatable<'a> with
        member this.Value with get() = v and set(v) = this.Value <- v

[<AbstractClass>]       
/// Base class which simplifies implementation of standard signals
type SignalBase<'a>(dependencies) as self =
    do
        dependencies
        |> Array.iter (fun (d : ITracksDependents) -> d.Track self)

    let dependencies = Dependencies.create dependencies self
    let mutable dirty = false

    // We have a signal guard in place to prevent stackoverflows.
    // If a Signal isn't referentially transparent, it's possible that a signal
    // can trigger the value to not match the last value, which in turn signals again,
    // which forms infinite loops.  This is only a problem in practice if you build
    // a mapping which uses randomization, but impacted a user. Guarding
    // prevents a single signal from signaling their dependencies more than once in
    // any signal change, effectively trampolining the notification to prevent multiple
    // triggers from occurring
    let mutable signalGuard = false

    /// Signals to dependencies that we have updated
    abstract member MarkDirtyGuarded : obj -> unit
    default this.MarkDirtyGuarded _ = 
        if not signalGuard then
            signalGuard <- true
            dependencies.MarkDirty this |> ignore
            signalGuard <- false
   
    /// Update and fetch the current value.  Implementers should only update if we're dirty.
    abstract member UpdateAndGetCurrentValue : updateRequired : bool -> 'a    

    /// Gets the current value
    member this.Value 
        with get() : 'a = 
            let updateRequired = 
                if dirty then
                    dirty <- false
                    true
                else
                    false
            this.UpdateAndGetCurrentValue updateRequired

    member __.Dirty with get() = dirty and set(v) = dirty <- v
                
    /// Notifies us that we need to refresh our value
    abstract member MarkDirty : obj -> unit
    default this.MarkDirty source =
        if (not this.Dirty) then
            this.Dirty <- true
            this.MarkDirtyGuarded source

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
        member this.UpdateDirtyFlag obj = this.MarkDirty obj
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

/// Type to wrap in observable into a signal.
type internal ObservableToSignal<'a>(valueProvider : IObservable<'a>, initialValue) as self =
    let dependencies = Dependencies.create [| |] self
    let mutable lastValue = initialValue

    // Wrap this in an option so we can stop referencing it on disposal
    let mutable valueProvider = Some valueProvider

    // Create a weak subscription to the observable to update us.
    // This is used until we get a subscriber that tracks us, in which case we switch to a strong
    // subscription
    static let subscribeWeak (t: ObservableToSignal<'a>) (obs : IObservable<'a>) =
        let reference = WeakReference(t)
        let mutable sub : IDisposable = null
        sub <-
            obs.Subscribe ( fun v ->
                let target = reference.Target
                match target with
                | null -> sub.Dispose()
                | t -> 
                    ObservableToSignal.SubscriptionOnNext (unbox t) v )
        sub    

    let mutable weakSubscription = subscribeWeak self valueProvider.Value    
    let mutable signalGuard = false

    static member SubscriptionOnNext (target : ObservableToSignal<'a>) value =
        target.UpdateValue value

    /// Signals to dependencies that we have updated
    member this.Signal () = 
        if not signalGuard then
            signalGuard <- true
            dependencies.MarkDirty this |> ignore
            signalGuard <- false
    
    member private this.UpdateValue v = 
        if not <| EqualityComparer<'a>.Default.Equals(lastValue, v) then
            lastValue <- v
            this.Signal()

    /// Gets the current value
    member __.Value with get () = lastValue

    /// Notifies us that we need to refresh our value
    member __.RequestRefresh _ = ()

    /// Default implementations work off single set of dependenices
    member __.HasDependencies with get() = dependencies.HasDependencies

    override this.Finalize() =
        (this :> IDisposable).Dispose()        

    interface ISignal<'a> with
        member this.Value with get() = this.Value

    interface IDependent with
        member this.UpdateDirtyFlag obj = this.RequestRefresh obj
        member this.HasDependencies with get() = this.HasDependencies

    interface IObservable<'a> with
        member this.Subscribe obs = dependencies.Subscribe (obs,this)

    interface ITracksDependents with
        member this.Track dep = 
            dependencies.Add (dep,this)            
        member this.Untrack dep = 
            dependencies.Remove (dep,this)            

    interface IDisposable with
        member this.Dispose () =            
            dependencies.RemoveAll this
            weakSubscription.Dispose()            
            weakSubscription <- null
            valueProvider <- None
            GC.SuppressFinalize this

type internal MappingSignal<'a,'b>(valueProvider : ISignal<'a>, mapping : 'a -> 'b, disposeProviderOnDispose : bool) =
    inherit SignalBase<'b>([| valueProvider |])
        
    let mutable lastValue = mapping valueProvider.Value
    let mutable valueProvider = Some(valueProvider)

    override this.UpdateAndGetCurrentValue updateRequired =
        if updateRequired then
            let value = 
                DisposeHelpers.getValue valueProvider (fun _ -> this.GetType().FullName)
                |> mapping
            if not <| EqualityComparer<_>.Default.Equals(lastValue, value) then
                lastValue <- value     
        lastValue      
   
    override this.OnDisposing () =
        this |> DisposeHelpers.cleanup &valueProvider disposeProviderOnDispose 

type internal ObserveOnSignal<'a>(valueProvider : ISignal<'a>, ctx : System.Threading.SynchronizationContext) =
    inherit MappingSignal<'a,'a>(valueProvider, id, false)

    member private __.MarkDirtyBase source = base.MarkDirtyGuarded source
    override this.MarkDirtyGuarded source = ctx.Post (System.Threading.SendOrPostCallback(fun _ -> this.MarkDirtyBase source), null)

type internal Mapping2Signal<'a,'b,'c>(valueProvider1 : ISignal<'a>, valueProvider2 : ISignal<'b>, mapping : 'a -> 'b -> 'c) =
    inherit SignalBase<'c>([| valueProvider1 ; valueProvider2 |])

    let mutable lastValue = mapping valueProvider1.Value valueProvider2.Value
    let mutable valueProvider1 = Some(valueProvider1)
    let mutable valueProvider2 = Some(valueProvider2)

    override this.UpdateAndGetCurrentValue updateRequired =
        if updateRequired then
            let value = 
                let v1 = DisposeHelpers.getValue valueProvider1 (fun _ -> this.GetType().FullName)
                let v2 = DisposeHelpers.getValue valueProvider2 (fun _ -> this.GetType().FullName)
                mapping v1 v2
            if not <| EqualityComparer<_>.Default.Equals(lastValue, value) then
                lastValue <- value
                this.MarkDirtyGuarded this
        lastValue    

    override this.OnDisposing () =
        this |> DisposeHelpers.cleanup &valueProvider1 false
        this |> DisposeHelpers.cleanup &valueProvider2 false 

type internal MergeSignal<'a>(valueProvider1 : ISignal<'a>, valueProvider2 : ISignal<'a>) =
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
        // We always flag ourself clean
        this.Dirty <- false
        if (valueProvider1.IsSome) then
            let value = value()
            if not <| EqualityComparer<_>.Default.Equals(lastValue, value) then
                lastValue <- value
                base.MarkDirtyGuarded this
    
    override __.UpdateAndGetCurrentValue _ = lastValue

    // Specifically always trigger an udpate for merge
    override this.MarkDirty obj = this.Update obj

    override this.OnDisposing () =
        this |> DisposeHelpers.cleanup &valueProvider1 false 
        this |> DisposeHelpers.cleanup &valueProvider2 false 

type internal IfSignal<'a>(valueProvider : ISignal<'a>, initialValue, conditionProvider : ISignal<bool>) =
    inherit SignalBase<'a>([| valueProvider ; conditionProvider |])

    let mutable lastValue = if conditionProvider.Value then valueProvider.Value else initialValue

    let mutable valueProvider = Some(valueProvider)
    let mutable conditionProvider = Some(conditionProvider)

    override this.UpdateAndGetCurrentValue updateRequired =
        if updateRequired then
            let value = 
                let condition = DisposeHelpers.getValue conditionProvider (fun _ -> this.GetType().FullName)            
                if condition then
                    DisposeHelpers.getValue valueProvider (fun _ -> this.GetType().FullName)
                else
                    lastValue

            if not <| EqualityComparer<_>.Default.Equals(lastValue, value) then
                lastValue <- value
        lastValue
    

    override this.OnDisposing () =
        this |> DisposeHelpers.cleanup &valueProvider false 
        this |> DisposeHelpers.cleanup &conditionProvider false

type internal FilteredSignal<'a> (valueProvider : ISignal<'a>, initialValue : 'a, filter : 'a -> bool, disposeProviderOnDispose : bool) =
    inherit SignalBase<'a>([| valueProvider |])

    let mutable lastValue = if filter(valueProvider.Value) then valueProvider.Value else initialValue

    let mutable valueProvider = Some(valueProvider)    

    override this.UpdateAndGetCurrentValue updateRequired =
        if updateRequired then
            match valueProvider with
            | None -> ()
            | Some provider ->
                let value = provider.Value                
                if (filter(value)) then
                    if not <| EqualityComparer<'a>.Default.Equals(lastValue, value) then
                        lastValue <- value
        lastValue

    override this.OnDisposing () =
        this |> DisposeHelpers.cleanup &valueProvider disposeProviderOnDispose 
                        
type internal ChooseSignal<'a,'b>(valueProvider : ISignal<'a>, initialValue : 'b, filter : 'a -> 'b option) =
    inherit SignalBase<'b>([| valueProvider |])

    let mutable lastValue = 
        match filter(valueProvider.Value) with
        | Some v -> v
        | None -> initialValue

    let mutable valueProvider = Some(valueProvider)
    

    override this.UpdateAndGetCurrentValue updateRequired =
        match valueProvider with
        | None -> ()
        | Some provider ->
            let value = provider.Value                
            match (filter(value)) with
            | Some newValue ->
                if not <| EqualityComparer<'b>.Default.Equals(lastValue, newValue) then
                    lastValue <- newValue
            | None -> ()
        lastValue

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
                this.MarkDirtyGuarded this)
        |> ignore

    override __.UpdateAndGetCurrentValue _ = lastValue

    override this.MarkDirty _ = this.Update ()

    override this.OnDisposing () =
        handle
        |> WeakRef.execute (fun v ->
            v.Untrack this                    
            handle.SetTarget(Unchecked.defaultof<ISignal<'a>>))
        |> ignore   

namespace Gjallarhorn.Helpers

open Gjallarhorn.Internal

/// Type which tracks execution, used for tracked async operations
/// Acts as a ISignal&lt;bool&gt; with value of true when idle, false when executing
type IdleTracker(ctx : System.Threading.SynchronizationContext) =
    inherit SignalBase<bool>([| |])

    let handles = ResizeArray<_>()
        
    member private this.AddHandle h =
        lock handles (fun _ ->
            handles.Add h
            this.MarkDirtyGuarded this   
        )
    member private this.RemoveHandle h =
        lock handles (fun _ ->
            if handles.Remove h then this.MarkDirtyGuarded this
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

    member private this.MarkDirtyBase source = base.MarkDirtyGuarded source
    override this.MarkDirtyGuarded source = 
        match ctx with
        | null -> this.MarkDirtyBase source
        | _ -> ctx.Post (System.Threading.SendOrPostCallback(fun _ -> this.MarkDirtyBase source), null)
    
    override __.UpdateAndGetCurrentValue _ = lock handles (fun _ -> handles.Count = 0)    
    override __.OnDisposing () = ()

namespace Gjallarhorn.Internal

open Gjallarhorn
open Gjallarhorn.Helpers
open System
open System.Collections.Generic

type internal AsyncMappingSignal<'a,'b>(valueProvider : ISignal<'a>, initialValue : 'b, tracker: IdleTracker option, mapFn : 'a -> Async<'b>, ?cancellationToken : System.Threading.CancellationToken) as self =
    inherit SignalBase<'b>([| valueProvider |])

    let mutable lastValue = initialValue

    do
        // We need to subscribe to changes immediately here,
        // Since this acts like a cache
        (valueProvider :> Internal.ITracksDependents).Track self

    let mutable valueProvider = Some(valueProvider)    
    let ctx = System.Threading.SynchronizationContext.Current

    member private this.Update () =
        let inputValue = 
            DisposeHelpers.getValue valueProvider (fun _ -> this.GetType().FullName)

        let exec =             
            async {
                let _execHandle = 
                    tracker 
                    |> Option.map (fun t -> t.GetExecutionHandle()) 
                let releaseHandle () =
                    _execHandle |> Option.iter (fun h -> h.Dispose())
                    
                let! result = mapFn(inputValue)

                if not <| EqualityComparer<_>.Default.Equals(lastValue, result) then    
                    if (ctx <> null) then
                        do! Async.SwitchToContext ctx
                    releaseHandle()
                    lastValue <- result
                    this.MarkDirty ()    
                else
                    releaseHandle()
            }
        
        Async.Start(exec, defaultArg cancellationToken System.Threading.CancellationToken.None )

    override __.UpdateAndGetCurrentValue _ = lastValue    

    override this.MarkDirty _ = this.Update ()

    override this.OnDisposing () =
        this |> DisposeHelpers.cleanup &valueProvider false
