namespace Gjallarhorn

open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System

/// Provides mechanisms for working with IView views
module View =
    
    /// Create a view over a constant, immutable value
    let constant (value : 'a) = 
        {
            new IView<'a> with
                member __.Value = value

                // A constant never changes/signals, so do nothing for these
                member __.AddDependency _ _ =
                    ()
                member __.RemoveDependency _ _ =
                    ()
                member __.Signal () =
                    ()
        }

    /// <summary>Create a cached view over a provider</summary>
    /// <remarks>
    /// This will not hold a reference to the provider, and will allow it to be garbage collected.
    /// As such, it caches the "last valid" state of the view locally.
    /// </remarks>
    let cache (provider : IView<'a>) = 
        new CachedView<'a>(provider) :> IDisposableView<'a>

    /// Create a view from an observable.  As an IView always provides a value, the initial value to use upon creation is required     
    let fromObservable initialValue (observable : IObservable<'a>) =
        let value = Mutable.create initialValue        
        let disposable = observable.Subscribe (fun v -> value.Value <- v)
        
        // Return a wrapper around a mutable that changes when the observable changes
        let rec dependent = {
            new IDisposableView<'a> with
                member __.Value = value.Value
                member this.AddDependency mechanism dep =
                    // TODO: Should this always be weak?
                    SignalManager.AddDependency this dep                
                member this.RemoveDependency mechanism dep =
                    SignalManager.RemoveDependency this dep
                member this.Signal () =
                    SignalManager.Signal(this)
            interface IDependent with
                member __.RequestRefresh _ = 
                    SignalManager.Signal dependent                
            interface IDisposable with
                member __.Dispose() =
                    disposable.Dispose()
                    value.RemoveDependency DependencyTrackingMechanism.Default (dependent :?> IDependent)
                    SignalManager.RemoveAllDependencies dependent
        }

        value.AddDependency DependencyTrackingMechanism.Default (dependent :?> IDependent)
        dependent

    /// Create a subscription to the changes of a view which calls the provided function upon each change
    let subscribe (f : 'a -> unit) (provider : IView<'a>) = 
        let rec dependent =
            {
                new IDependent with
                    member __.RequestRefresh _ =
                        f(provider.Value)
                interface IDisposable with
                    member __.Dispose() = 
                        provider.RemoveDependency DependencyTrackingMechanism.Default dependent
            }
        provider.AddDependency DependencyTrackingMechanism.Default dependent
        dependent :> IDisposable
    
    /// Gets the current value associated with the view
    let get (view : IView<'a>) = 
        view.Value

    /// Executes a function for a view value.
    let iter (f : 'a -> unit)  (view : IView<'a>)=
        f(view.Value)

    /// Transforms a view value by using a specified mapping function.
    let map (mapping : 'a -> 'b)  (provider : IView<'a>) = 
        let view = new MappingView<'a, 'b>(provider, mapping, false)
        view :> IDisposableView<'b>

    /// Transforms two view values by using a specified mapping function.
    let map2 (mapping : 'a -> 'b -> 'c) (provider1 : IView<'a>) (provider2 : IView<'b>) = 
        let view = new Mapping2View<'a, 'b, 'c>(provider1, provider2, mapping)
        view :> IDisposableView<'c>

    /// Filters the view, so only values matching the predicate are cached and propogated onwards
    let filter (predicate : 'a -> bool) (provider : IView<'a>) =
        let view = new FilteredView<'a>(provider, predicate, false)
        view :> IDisposableView<'a>

    /// Need a description
    let choose (predicate : 'a -> 'b option) (provider : IView<'a>) =        
        let map = new MappingView<'a,'b option>(provider, predicate, false)
        let filter = new FilteredView<'b option>(map, (fun v -> v <> None), true)
        let view = new MappingView<'b option, 'b>(filter, (fun opt -> opt.Value), true)
        view :> IDisposableView<'b>

    /// Applies a View of a function in order to provide mapping of arbitrary arity
    let apply (mappingView : IView<'a -> 'b>) provider =        
        let view = new Mapping2View<'a->'b, 'a, 'b>(mappingView, provider, (fun a b -> a b))
        view :> IDisposableView<'b>

    /// <summary>Converts any IView into an IObservable</summary>
    /// <remarks>The result can be Disposed to stop tracking</remarks>
    let asObservable (view : IView<'a>) =
        new Observer<'a>(view) :> IDisposableObservable<'a>

    /// Custom computation expression builder for composing IView instances dynamically
    type ViewBuilder() =        
        /// Called for let! in computation expression to extract the value from a view
        member __.Bind(view : IView<'a>, f : 'a -> IView<'b>) = 
            let unwrap value = f(value).Value
            // TODO: Should this "dispose" the calling view somehow?
            map unwrap view :> IView<'b>
    
        /// Called for return in computation expressions to recompose the view.
        member __.Return (v : 'a) =
            // TODO: Should this "dispose" the calling view somehow?
            constant v

    /// <summary>Create a computation expression you can use to compose multiple views</summary>
    /// <remarks>The main disadvantage to this approach is that the resulting views are not all disposable
    /// and rely on the GC to clean up the subscriptions.</remarks>
    let view = ViewBuilder()

    type internal ValidatorMappingView<'a>(validator : ValidationCollector<'a> -> ValidationCollector<'a>, valueProvider : IView<'a>) =
        inherit MappingView<'a,'a>(valueProvider, id, true)

        let validateCurrent () =
            validate valueProvider.Value
            |> validator
            |> Validation.result
        let validationResult = 
            validateCurrent()
            |> Mutable.create

        let subscriptionHandle =
            let rec dependent =
                {
                    new IDependent with
                        member __.RequestRefresh _ =
                            validationResult.Value <- validateCurrent()
                    interface System.IDisposable with
                        member __.Dispose() = 
                            valueProvider.RemoveDependency DependencyTrackingMechanism.Default dependent
                }
            valueProvider.AddDependency DependencyTrackingMechanism.Default dependent
            dependent :> System.IDisposable

        member private __.EditAndValidate value =  
            validationResult.Value <- validateCurrent()
            value

        override __.Disposing() =
            subscriptionHandle.Dispose()
            SignalManager.RemoveAllDependencies validationResult

        interface IValidatedView<'a> with
            member __.ValidationResult with get() = validationResult :> IView<ValidationResult>

            member __.IsValid = isValid validationResult.Value

    /// Validates a view with a validation chain
    let validate<'a> (validator : ValidationCollector<'a> -> ValidationCollector<'a>) (view : IView<'a>) =
        new ValidatorMappingView<'a>(validator, view) :> IValidatedView<'a>
    
[<AutoOpen>]
/// Custom operators for composing IView instances
module ViewOperators =
    /// Performs the application, allowing for View.constant someFunUsingABC <*> a <*> b <*> c
    let ( <*> ) (f : IView<'a->'b>) (x : IView<'a>) : IView<'b> = View.apply f x :> IView<'b>
