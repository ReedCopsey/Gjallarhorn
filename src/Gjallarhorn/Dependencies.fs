namespace Gjallarhorn.Internal

open Gjallarhorn

open System
open System.Runtime.CompilerServices

module WeakRef =
    let toOption (wr : WeakReference<_>) =
        match wr.TryGetTarget() with
        | true, t -> Some t
        | false, _ -> None

    let execute (f : 'a -> unit) (wr : WeakReference<'a>) =
        match wr.TryGetTarget() with
        | true, t ->
            f(t)
            true
        | _ -> false

    let test (f : 'a -> bool) (wr : WeakReference<'a>) =
        match wr.TryGetTarget() with
        | true, t ->
            true, f(t)
        | _ -> 
            false, false

////////////////////////////////////////////////////////
// This file contains the basic implementations used
// for tracking dependencies between objects.
// The module exposes the functions required to create
// the appropriate types, and is all that should be used
// directly from here.
////////////////////////////////////////////////////////

/// Type used to track dependencies
type [<AllowNullLiteral>] IDependencyManager<'a> =
    /// Add a dependent to the source signal explicitly
    abstract member Add : IDependent * ISignal<'a> -> unit

    /// Add a dependent observer to this signal explicitly
    abstract member Subscribe : System.IObserver<'a> * ISignal<'a> -> IDisposable
    
    /// Remove a dependent from this signal explicitly
    abstract member Remove : IDependent * ISignal<'a> -> unit

    /// Remove all dependencies from this signal
    abstract member RemoveAll : ISignal<'a> -> unit

    /// Signal to all dependents to refresh themselves
    abstract member Signal : ISignal<'a> -> unit

    /// Determines whether there are dependencies currently being managed
    abstract member HasDependencies : bool with get

[<AbstractClass;AllowNullLiteral>]
type internal DependencyTrackerBase() =
    do ()
/// <summary>Used to track dependencies</summary>
/// <remarks>This class is fully thread safe, and will not hold references to dependent targets</remarks>
[<AllowNullLiteral>]
type internal DependencyTracker<'a>(dependsOn : WeakReference<ITracksDependents> array) =
    inherit DependencyTrackerBase()
    
    // We want this as lightweight as possible,
    // so we do our own management as needed
    let mutable depIDeps : WeakReference<IDependent> array = [| |]
    let mutable depObservers : WeakReference<IObserver<'a>> array = [| |]
    let mutable trackingUpstream = false
        
    // These are ugly, as it purposefully creates side effects
    // It returns true if we signaled and the object is alive,
    // otherwise false
    let signalIfAliveDep (wr: WeakReference<IDependent>) =
        wr |> WeakRef.execute (fun dep -> dep.RequestRefresh())
    let signalIfAliveObs value (wr: WeakReference<IObserver<'a>>) =
        wr |> WeakRef.execute (fun obs-> obs.OnNext(value))

    // Do our signal, but also remove any unneeded dependencies while we're at it
    let signalAndUpdateDependencies value =
        depIDeps <- depIDeps |> Array.filter signalIfAliveDep
        depObservers <- depObservers |> Array.filter (signalIfAliveObs value)

    // Remove a dependency, as well as all "dead" dependencies
    let removeAndFilterDep dep (wr : WeakReference<IDependent>) =
        match WeakRef.test ( (=) dep) wr with
        | false, _ -> false
        | true, true -> false
        | true, false -> true
    let removeAndFilterObs obs (wr : WeakReference<IObserver<'a>>) =        
        match WeakRef.test ( (=) obs) wr with
        | false, _ -> false
        | true, true -> 
            // Mark observer completed
            obs.OnCompleted()
            false
        | true, false -> true
    let markObsComplete (wr : WeakReference<IObserver<'a>>) =
        wr 
        |> WeakRef.execute (fun obs-> obs.OnCompleted())
        |> ignore        
    
    member private this.UpdateUpstreamTracking source =
        match this.HasDependencies, trackingUpstream with
        | true, true -> ()
        | true, false -> 
            dependsOn
            |> Seq.choose WeakRef.toOption
            |> Seq.iter (fun d -> d.Track source)
            trackingUpstream <- true
        | false, false -> ()
        | false, true ->
            // Stop tracking
            dependsOn
            |> Seq.choose WeakRef.toOption
            |> Seq.iter (fun d -> d.Untrack source)
            trackingUpstream <- false


    member private __.LockObj with get() = depIDeps // Always lock on this array

    /// determines whether there are currently any dependencies on this object
    member this.HasDependencies with get() = lock this.LockObj (fun _ -> depIDeps.Length + depObservers.Length > 0)

    /// Adds a new dependency to the tracker
    member this.Add (dep,source) =
        lock this.LockObj (fun _ ->
            depIDeps <- depIDeps |> Array.append [| WeakReference<_>(dep) |]
            this.UpdateUpstreamTracking source)
    member this.AddObserver (obs,source) =
        lock this.LockObj (fun _ ->
            depObservers <- depObservers |> Array.append [| WeakReference<_>(obs) |]
            this.UpdateUpstreamTracking source)

    /// Removes a dependency from the tracker, and returns true if there are still dependencies remaining
    member this.Remove (dep, source) = 
        lock this.LockObj (fun _ ->
            depIDeps <- depIDeps |> Array.filter (removeAndFilterDep dep)
            this.UpdateUpstreamTracking source
            this.HasDependencies)
    member this.RemoveObserver (obs, source) = 
        lock this.LockObj (fun _ ->
            depObservers <- depObservers |> Array.filter (removeAndFilterObs obs)
            this.UpdateUpstreamTracking source
            this.HasDependencies)

    /// Removes a dependency from the tracker, and returns true if there are still dependencies remaining
    member this.RemoveAll source = 
        lock this.LockObj 
            (fun _ -> 
                depIDeps <- [| |]
                depObservers
                |> Array.iter markObsComplete
                depObservers <- [| |]
                this.UpdateUpstreamTracking source)

    /// Signals the dependencies with a given source, and returns true if there are still dependencies remaining
    member this.Signal (source : ISignal<'a>) = 
        lock this.LockObj (fun _ ->
            signalAndUpdateDependencies source.Value
            this.HasDependencies)

    interface IDependencyManager<'a> with
        member this.Add (dep: IDependent, source: ISignal<'a>) = this.Add (dep,source)
        member this.Subscribe (dep: IObserver<'a>, source: ISignal<'a>) =             
            this.AddObserver (dep, source)
            { 
                new IDisposable with
                    member __.Dispose() = this.RemoveObserver (dep, source) |> ignore
            }        
        member this.Remove (dep: IDependent, source: ISignal<'a>) = ignore <| this.Remove (dep,source)
        member this.RemoveAll (source: ISignal<'a>) = this.RemoveAll source
        member this.Signal source = ignore <| this.Signal source
        member this.HasDependencies with get() = this.HasDependencies

/// <summary>Manager of all dependency tracking.  Handles signaling of IDependent instances from any given source</summary>
/// <remarks>This class is fully thread safe, and will not hold references to either source or dependent targets</remarks>
[<AbstractClass; Sealed>]
type internal SignalManager() = // Note: Internal to allow for testing in memory tests
    static let dependencies = ConditionalWeakTable<obj, DependencyTrackerBase>()
    static let createValueCallbackFor (__ : ISignal<'a>) = ConditionalWeakTable<obj, DependencyTrackerBase>.CreateValueCallback((fun _ -> DependencyTracker<'a>([| |]) :> DependencyTrackerBase))

    static let remove source =
        lock dependencies (fun _ -> dependencies.Remove(source) |> ignore)

    static let tryGet (source : ISignal<'a>) =
        match dependencies.TryGetValue(source) with
        | true, dep -> true, dep :?> DependencyTracker<'a>
        | false, _ -> false, null
        

    /// Signals all dependencies tracked on a given source
    static member Signal (source : ISignal<'a>) =
        lock dependencies (fun _ ->
            let exists, dep = tryGet source
            if exists then             
                if not(dep.Signal(source)) then
                    remove source)
    
    /// Adds dependency tracked on a given source
    static member internal AddDependency (source : ISignal<'a>, target : IDependent) =
        lock dependencies (fun _ -> 
            let dep = dependencies.GetValue(source, createValueCallbackFor source) :?> DependencyTracker<'a>
            dep.Add(target,source))
    static member internal Subscribe (source : ISignal<'a>, target : IObserver<'a>) =
        lock dependencies (fun _ -> 
            let dep = dependencies.GetValue(source, createValueCallbackFor source) :?> DependencyTracker<'a>
            dep.AddObserver (target, source)
            { 
                new IDisposable with
                    member __.Dispose() = dep.RemoveObserver (target, source) |> ignore
            })

    /// Removes a dependency tracked on a given source
    static member internal RemoveDependency (source : ISignal<'a>, target : IDependent) =
        let removeDep () =
            match tryGet source with
            | true, dep ->
                if not(dep.Remove(target,source)) then
                    remove source
            | false, _ -> ()
        lock dependencies removeDep

    /// Removes all dependencies tracked on a given source
    static member RemoveAllDependencies (source : ISignal<'a>) =
        remove source

    /// Returns true if a given source has dependencies
    static member IsTracked (source : ISignal<'a>) =
        lock dependencies (fun _ -> fst <| dependencies.TryGetValue(source))

/// Module used to create and manage dependencies
module Dependencies =
    /// Create a dependency manager
    let create (dependsOn : ITracksDependents array) source = 
        let deps =
            dependsOn
            |> Array.map (fun t -> WeakReference<_>(t))
        DependencyTracker<_>(deps) :> IDependencyManager<_>
    /// Create a dependency manager for a source object which stores dependency information outside of the object's memory space.  
    let createRemote source =
        { new IDependencyManager<'a> with
            member __.Add (dep: IDependent, source: ISignal<'a>) : unit = SignalManager.AddDependency(source, dep)
            member __.Subscribe (obs: IObserver<'a> , source: ISignal<'a>) : IDisposable = SignalManager.Subscribe(source, obs)

            member __.Remove (dep: IDependent, source: ISignal<'a>) = SignalManager.RemoveDependency(source, dep)
            member __.RemoveAll (source: ISignal<'a>) = SignalManager.RemoveAllDependencies source

            member __.HasDependencies with get() = SignalManager.IsTracked source

            member __.Signal source = SignalManager.Signal source
        }
