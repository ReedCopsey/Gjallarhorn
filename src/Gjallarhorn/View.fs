namespace Gjallarhorn

open System

/// Provides mechanisms for working with IView<'a> views
module View =
    
    /// Create a view over a constant, immutable value
    [<CompiledName("FromConstant")>]
    let constant (value : 'a) = 
        {
            new IView<'a> with
                member __.Value = value
        }

    /// Create a view over a provider given a specific mapping function
    [<CompiledName("Map")>]
    let map (provider : IView<'a>) (mapping : 'a -> 'b) = 
        let view = new View<'a, 'b>(provider, mapping)
        view :> IDisposableView<_>
    
    /// <summary>Create a cached view over a provider</summary>
    /// <remarks>
    /// This will not hold a reference to the provider, and will allow it to be garbage collected.
    /// As such, it caches the "last valid" state of the view locally.
    /// </remarks>
    [<CompiledName("Cache")>]
    let cache (provider : IView<'a>) = new ViewCache<'a>(provider) :> IDisposableView<_>

    /// Add a permanent subscription to the changes of a view which calls the provided function upon each change
    [<CompiledName("AddToView")>]
    let add (provider : IView<'a>) (f : 'a -> unit) = 
        let dependent =
            {
                new IDependent with
                    member __.RequestRefresh _ =
                        f(provider.Value)
                interface IDisposable with
                    member __.Dispose() = ()
            }
        SignalManager.AddDependency provider dependent        

    /// Create a subscription to the changes of a view which calls the provided function upon each change
    [<CompiledName("SubscribeToView")>]
    let subscribe (provider : IView<'a>) (f : 'a -> unit) = 
        let rec dependent =
            {
                new IDependent with
                    member __.RequestRefresh _ =
                        f(provider.Value)
                interface IDisposable with
                    member __.Dispose() = 
                        SignalManager.RemoveDependency provider dependent
            }
        SignalManager.AddDependency provider dependent
        dependent :> IDisposable

    /// Create a view from an observable.  As an IView always provides a value, the initial value to use upon creation is required     
    [<CompiledName("FromObservable")>]
    let fromObservable (observable : IObservable<'a>) initialValue =
        let value = Mutable.create initialValue        
        let disposable = observable.Subscribe (fun v -> value.Value <- v)
        
        // Return a wrapper around a mutable that changes when the observable changes
        {
            new IDisposableView<'a> with
                member __.Value = value.Value
            interface IDisposable with
                member __.Dispose() =
                    disposable.Dispose()
        }