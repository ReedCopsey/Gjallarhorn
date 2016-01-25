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
                member __.AddDependency _ =
                    ()
                member __.RemoveDependency _ =
                    ()
                member __.Signal () =
                    ()
        }

    /// Introduce arbitrary values into a view
    let pure' = constant


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
                member this.AddDependency dep =
                    SignalManager.AddDependency this dep                
                member this.RemoveDependency dep =
                    SignalManager.RemoveDependency this dep
                member this.Signal () =
                    SignalManager.Signal(this)
            interface IDependent with
                member __.RequestRefresh _ = 
                    SignalManager.Signal dependent                
            interface IDisposable with
                member __.Dispose() =
                    disposable.Dispose()
                    value.RemoveDependency (dependent :?> IDependent)
                    SignalManager.RemoveAllDependencies dependent
        }

        value.AddDependency (dependent :?> IDependent)
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
                        provider.RemoveDependency dependent
            }
        provider.AddDependency dependent
        dependent :> IDisposable
    
    /// Create a subscription to the changes of a view which copies its value upon change into a mutable
    let copyTo (target : IMutatable<'a>) (provider : IView<'a>) =
        let rec dependent =
            {
                new IDependent with
                    member __.RequestRefresh _ =
                        target.Value <- provider.Value
                interface IDisposable with
                    member __.Dispose() = 
                        provider.RemoveDependency dependent
            }
        provider.AddDependency dependent
        target.Value <- provider.Value
        dependent :> IDisposable

    /// Create a subscription to the changes of a view which copies its value upon change into a mutable via a stepping function
    let copyStep (target : IMutatable<'b>) (stepFunction : 'b -> 'a -> 'b) (provider : IView<'a>) =
        let update() =
            target.Value <- stepFunction target.Value provider.Value
        let rec dependent =
            {
                new IDependent with
                    member __.RequestRefresh _ = update()
                interface IDisposable with
                    member __.Dispose() = 
                        provider.RemoveDependency dependent
            }
        provider.AddDependency dependent
        update()
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

    /// Creates a view on two values that is true if both inputs are equal
    let equal a b =
        map2 (fun a b -> a = b) a b

    /// Creates a view on two values that is true if both inputs are not equal
    let notEqual a b =
        map2 (fun a b -> a <> b) a b
    
    
    /// Creates a view over a bool value that negates the input
    let not a =
        map (fun a -> not(a)) a
    
    /// Creates a view on two bools that is true if both inputs are true
    let both (a : IView<bool>) (b : IView<bool>) =
        map2 (fun a b -> a && b) a b

    /// Creates a view on two bools that is true if either input is true
    let either (a : IView<bool>) (b : IView<bool>) =
        map2 (fun a b -> a || b) a b

    /// <summary>Converts any IView into an IObservable</summary>
    /// <remarks>The result can be Disposed to stop tracking</remarks> 
    let asObservable (view : IView<'a>) =
        new Observer<'a>(view) :> IDisposableObservable<'a>

    type internal ValidatorMappingView<'a>(validator : ValidationCollector<'a> -> ValidationCollector<'a>, valueProvider : IView<'a>) =
        inherit MappingView<'a,'a>(valueProvider, id, true)

        let validateCurrent () =
            validate valueProvider.Value
            |> validator
            |> Validation.result
        let validationResult = 
            validateCurrent()
            |> Mutable.create
        
        override __.Refreshing() =
            validationResult.Value <- validateCurrent()
        override __.Disposing() =
            SignalManager.RemoveAllDependencies validationResult

        interface IValidatedView<'a> with
            member __.ValidationResult with get() = validationResult :> IView<ValidationResult>

            member __.IsValid = isValid validationResult.Value

    /// Validates a view with a validation chain
    let validate<'a> (validator : ValidationCollector<'a> -> ValidationCollector<'a>) (view : IView<'a>) =
        new ValidatorMappingView<'a>(validator, view) :> IValidatedView<'a>

    // Apply in reverse to allow for easy piping in liftN
    let private applyR provider (mappingView : IView<'a -> 'b>) =        
        let view = new Mapping2View<'a->'b, 'a, 'b>(mappingView, provider, (fun a b -> a b))
        view :> IDisposableView<'b>
    
    /// Combines two views using a specified function, equivelent to View.map2
    let lift2 f a b = map2 f a b
            
    /// Combines three views using a specified function
    let lift3 f a b c = 
        pure' f
        |> applyR a
        |> applyR b       
        |> applyR c
    
    /// Combines four views using a specified function
    let lift4 f a b c d = 
        lift3 f a b c
        |> applyR d
    
    /// Combines five views using a specified function
    let lift5 f a b c d e = 
        lift4 f a b c d
        |> applyR e
        
    /// Combines six views using a specified function
    let lift6 f a b c d e f' = 
        lift5 f a b c d e
        |> applyR f'

[<AutoOpen>]
/// Custom operators for composing IView instances
module ViewOperators =
    /// Applies the function inside the applicative functor, allowing for: View.pure' someFunUsingABC <*> a <*> b <*> c
    let ( <*> ) (f : IView<'a->'b>) (x : IView<'a>) : IView<'b> = View.apply f x :> IView<'b>

    /// Lifts the function into the applicative functor via View.pure', allowing for: someFunUsingABC <!> a <*> b <*> c
    let (<!>) f a = View.pure' f <*> a
