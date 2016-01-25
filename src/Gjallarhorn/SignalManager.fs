namespace Gjallarhorn.Internal

open Gjallarhorn

open System
open System.Runtime.CompilerServices

/// <summary>Used to track dependencies</summary>
/// <remarks>This class is fully thread safe, and will not hold references to dependent targets</remarks>
type DependencyTracker() =
    // We want this as lightweight as possible,
    // so we do our own management as needed
    let mutable dependencies : WeakReference<IDependent> array = [| |]

    // This is ugly, as it purposefully creates side effects
    // It returns true if we signaled and the object is alive,
    // otherwise false
    let signalIfAlive source (wr: WeakReference<IDependent>) =
        let success, dep = wr.TryGetTarget() 
        if success then dep.RequestRefresh(source)        
        success

    // Do our signal, but also remove any unneeded dependencies while we're at it
    let signalAndUpdateDependencies source =
        dependencies <- dependencies |> Array.filter (signalIfAlive source)

    // Remove a dependency, as well as all "dead" dependencies
    let removeAndFilter dep (wr : WeakReference<IDependent>) =
        match wr.TryGetTarget() with
        | false, _ -> false
        | true, v when obj.ReferenceEquals(v, dep) -> false
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

    /// Signals the dependencies with a given source, and returns true if there are still dependencies remaining
    member __.Signal (source : IView<'a>) = 
        lock dependencies (fun _ ->
            signalAndUpdateDependencies source
            dependencies.Length > 0)

/// <summary>Manager of all dependency tracking.  Handles signaling of IDependent instances from any given source</summary>
/// <remarks>This class is fully thread safe, and will not hold references to either source or dependent targets</remarks>
[<AbstractClass; Sealed>]
type SignalManager() =
    static let dependencies = ConditionalWeakTable<obj, DependencyTracker>()
    static let createValueCallback = ConditionalWeakTable<obj, DependencyTracker>.CreateValueCallback((fun _ -> DependencyTracker()))

    static let remove source =
        lock dependencies (fun _ -> dependencies.Remove(source) |> ignore)

    /// Signals all dependencies tracked on a given source
    static member Signal (source : IView<'a>) =
        lock dependencies (fun _ ->
            let exists, dep = dependencies.TryGetValue(source)
            if exists then             
                if not(dep.Signal(source)) then
                    remove source)
    
    /// Adds dependency tracked on a given source
    static member AddDependency (source : IView<'a>) target =
        lock dependencies (fun _ -> 
            let dep = dependencies.GetValue(source, createValueCallback)
            dep.Add target)

    /// Removes a dependency tracked on a given source
    static member RemoveDependency (source : IView<'a>) target =
        let removeDep () =
            match dependencies.TryGetValue(source) with
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