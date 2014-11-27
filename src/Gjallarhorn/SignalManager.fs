namespace Gjallarhorn.Internal

open Gjallarhorn

open System
open System.Runtime.CompilerServices

// Used to track dependency information
type internal DependencyInformation() =
    let mutable dependencies = ResizeArray<_>()

    // This is ugly, as it purposefully creates side effects
    // It returns false if we signaled and the object is alive,
    // otherwise true
    let signalIfAlive source (wr: WeakReference<IDependent>) =
        let success, dep = wr.TryGetTarget() 
        if success then dep.RequestRefresh(source)        
        not success
            
    // This signals using the above function, and removes any garbage collected dependencies
    let signalAndFilter (source : IView<'a>) =
        dependencies.RemoveAll((fun wr -> signalIfAlive source wr)) |> ignore
        dependencies.Count > 0

    // Remove all empty items from the list, as well as our dependency
    let removeAndFilter dependency =
        dependencies.RemoveAll(
            (fun wr -> 
                match wr.TryGetTarget() with
                | false, _ -> true
                | true, v when obj.ReferenceEquals(v, dependency) -> true
                | _ -> false)) |> ignore
        dependencies.Count > 0

    // Add a dependent object to be signaled
    member this.AddDependency (dependency : IDependent) =
        lock this (fun _ -> dependencies.Add(WeakReference<IDependent>(dependency)))

    // Add a dependent object to be signaled
    member this.RemoveDependency (dependency : IDependent) =
        lock this (fun _ -> removeAndFilter dependency)
    
    // Signal all dependencies that they should refresh themselves
    member this.Signal (source : IView<'a>) =
        // Only keep "living" dependencies
        lock this (fun _ -> signalAndFilter source)

/// <summary>Manager of all dependency tracking.  Handles signaling of IDependent instances from any given source</summary>
/// <remarks>This class is fully thread safe, and will not hold references to either source or dependent targets</remarks>
[<AbstractClass; Sealed>]
type internal SignalManager() =
    static let dependencies = ConditionalWeakTable<obj, DependencyInformation>()
    static let createValueCallback = ConditionalWeakTable<obj, DependencyInformation>.CreateValueCallback((fun _ -> DependencyInformation()))

    static let remove source =
        lock dependencies (fun _ -> dependencies.Remove(source) |> ignore)

    static member Signal (source : IView<'a>) =
        let exists, dep = dependencies.TryGetValue(source)
        lock dependencies (fun _ ->
            if exists then             
                if not(dep.Signal(source)) then
                    remove source)

    static member AddDependency (source : IView<'a>) target =
        lock dependencies (fun _ -> 
            let dep = dependencies.GetValue(source, createValueCallback)
            dep.AddDependency target)

    static member RemoveDependency (source : IView<'a>) target =
        let dep = dependencies.GetValue(source, createValueCallback)
        lock dependencies (fun _ -> 
            if not(dep.RemoveDependency target) then
                remove source)

    static member RemoveAllDependencies (source : IView<'a>) =
        remove source

    static member IsTracked (source : IView<'a>) =
        let exists, dep = dependencies.TryGetValue(source)
        exists