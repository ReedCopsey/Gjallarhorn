namespace Gjallarhorn.Internal

open Gjallarhorn

open System
open System.Runtime.CompilerServices

[<AbstractClass;AllowNullLiteral>]
type private DependencyTrackerBase() =
    do ()
/// <summary>Used to track dependencies</summary>
/// <remarks>This class is fully thread safe, and will not hold references to dependent targets</remarks>
[<AllowNullLiteral>]
type private DependencyTracker<'a>() =
    inherit DependencyTrackerBase()

    // We want this as lightweight as possible,
    // so we do our own management as needed
    let mutable dependencies : WeakReference<Dependency<'a>> array = [| |]

    let signal (dep : Dependency<_>) source =
        match dep with
        | View(dep') -> dep'.RequestRefresh source
        | Observer(obs) -> obs.OnNext(source.Value)

    // This is ugly, as it purposefully creates side effects
    // It returns true if we signaled and the object is alive,
    // otherwise false
    let signalIfAlive source (wr: WeakReference<Dependency<'a>>) =
        let success, dep = wr.TryGetTarget() 
        if success then signal dep source
        success

    // Do our signal, but also remove any unneeded dependencies while we're at it
    let signalAndUpdateDependencies source =
        dependencies <- dependencies |> Array.filter (signalIfAlive source)

    // Remove a dependency, as well as all "dead" dependencies
    let removeAndFilter dep (wr : WeakReference<Dependency<_>>) =
        match wr.TryGetTarget() with
        | false, _ -> false
        | true, v when v = dep -> 
            match v with
            | Observer(o) -> 
                // Mark observers as being completed when removed
                o.OnCompleted()
            | _ -> ()            
            false
        | _ -> true

    /// Adds a new dependency to the tracker
    member __.Add dep =
        lock dependencies (fun _ ->
            dependencies <- dependencies |> Array.append [| WeakReference<_>(dep) |])

    /// Removes a dependency from the tracker, and returns true if there are still dependencies remaining
    member __.Remove dep = 
        lock dependencies (fun _ ->
            dependencies <- dependencies |> Array.filter (removeAndFilter dep)
            dependencies.Length > 0)

    /// Removes a dependency from the tracker, and returns true if there are still dependencies remaining
    member __.RemoveAll () = 
        lock dependencies (fun _ -> dependencies <- [| |])

    /// Signals the dependencies with a given source, and returns true if there are still dependencies remaining
    member __.Signal (source : IView<'a>) = 
        lock dependencies (fun _ ->
            signalAndUpdateDependencies source
            dependencies.Length > 0)

    interface IDependencyManager<'a> with
        member this.Add dep = this.Add dep    
        member this.Remove dep = this.Remove dep |> ignore
        member this.RemoveAll () = this.RemoveAll()
        member this.Signal source = ignore <| this.Signal source

/// <summary>Manager of all dependency tracking.  Handles signaling of IDependent instances from any given source</summary>
/// <remarks>This class is fully thread safe, and will not hold references to either source or dependent targets</remarks>
[<AbstractClass; Sealed>]
type SignalManager() =
    static let dependencies = ConditionalWeakTable<obj, DependencyTrackerBase>()
    static let createValueCallbackFor (view : IView<'a>) = ConditionalWeakTable<obj, DependencyTrackerBase>.CreateValueCallback((fun _ -> DependencyTracker<'a>() :> DependencyTrackerBase))

    static let remove source =
        lock dependencies (fun _ -> dependencies.Remove(source) |> ignore)

    static let tryGet (source : IView<'a>) =
        match dependencies.TryGetValue(source) with
        | true, dep -> true, dep :?> DependencyTracker<'a>
        | false, _ -> false, null
        

    /// Signals all dependencies tracked on a given source
    static member Signal (source : IView<'a>) =
        lock dependencies (fun _ ->
            let exists, dep = tryGet source
            if exists then             
                if not(dep.Signal(source)) then
                    remove source)
    
    /// Adds dependency tracked on a given source
    static member AddDependency (source : IView<'a>) target =
        lock dependencies (fun _ -> 
            let dep = dependencies.GetValue(source, createValueCallbackFor source) :?> DependencyTracker<'a>
            dep.Add target)

    /// Removes a dependency tracked on a given source
    static member RemoveDependency (source : IView<'a>) target =
        let removeDep () =
            match tryGet source with
            | true, dep ->
                if not(dep.Remove target) then
                    remove source
            | false, _ -> ()
        lock dependencies removeDep

    /// Removes all dependencies tracked on a given source
    static member RemoveAllDependencies (source : IView<'a>) =
        remove source

    /// Returns true if a given source has dependencies
    static member IsTracked (source : IView<'a>) =
        lock dependencies (fun _ -> fst <| dependencies.TryGetValue(source))

type private RemoteDependencyMananger<'a>(source) =
    interface IDependencyManager<'a> with
        member this.Add dep = SignalManager.AddDependency source dep    
        member this.Remove dep = SignalManager.RemoveDependency source dep
        member this.Signal source = SignalManager.Signal source
        member this.RemoveAll () = SignalManager.RemoveAllDependencies source

/// Module used to create and manage dependencies
module Dependencies =
    /// Create a dependency manager
    let create () = 
        DependencyTracker<_>() :> IDependencyManager<_>
    /// Create a remote dependency manager
    let createRemote source =
        RemoteDependencyMananger<_>(source) :> IDependencyManager<_>