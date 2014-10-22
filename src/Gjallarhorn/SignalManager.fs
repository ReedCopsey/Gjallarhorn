namespace Gjallarhorn

open System
open System.Runtime.CompilerServices

/// Used to track dependency information
type internal DependencyInformation() =
    let mutable dependencies = ResizeArray<_>()

    // This is ugly, as it purposefully creates side effects
    // It returns true if we signaled and the object is alive,
    // otherwise false
    let signalIfAlive source (wr: WeakReference) =
        let dep = wr.Target :?> IDependent
        if dep <> null then dep.RequestRefresh(source)
            

    // This signals using the above function, and removes any garbage collected dependencies
    let signalAndFilter (source : IView<'a>) =
        // There is a (minor) race condition here - 
        // A dependency may get signaled *and* removed in the same call if the GC kicks in between these lines
        dependencies |> Seq.iter (signalIfAlive source)
        dependencies.RemoveAll((fun wr -> not wr.IsAlive)) |> ignore

    /// Add a dependent object to be signaled
    member this.AddDependency (dependency : IDependent) =
        lock this (fun _ -> dependencies.Add(WeakReference(dependency)))
    
    /// Signal all dependencies that they should refresh themselves
    member this.Signal (source : IView<'a>) =
        // Only keep "living" dependencies
        lock this (fun _ -> signalAndFilter source)

/// Manager of all dependency tracking.  Handles signaling of IDependent instances from any given source
/// Note: This class is fully thread safe, and will not hold references to either source or dependent targets
[<AbstractClass; Sealed>]
type internal SignalManager() =
    static let dependencies = ConditionalWeakTable<obj, DependencyInformation>()
    static let createValueCallback = ConditionalWeakTable<obj, DependencyInformation>.CreateValueCallback((fun _ -> DependencyInformation()))

    static member Signal (source : IView<'a>) =
        let exists, dep = dependencies.TryGetValue(source)
        if exists then dep.Signal(source)

    static member AddDependency (source : IView<'a>) target =
        let dep = dependencies.GetValue(source, createValueCallback)
        dep.AddDependency target

