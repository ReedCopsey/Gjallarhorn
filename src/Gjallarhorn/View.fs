namespace Gjallarhorn

#nowarn "40" "21"

open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System

/// Provides mechanisms for working with IView views
module View =
    
    /// Create a view over a constant, immutable value
    let constant (value : 'a) = 
        {
            // TODO: Should this use a dependency tracker anyways?  Right now, we always return false on has dependencies, but that's not accurate
            new IView<'a> with
                member __.Value = value
            interface IDependent with
                member __.RequestRefresh _ = ()
                member __.HasDependencies with get() = false
            interface ITracksDependents with
                member __.Track _ = ()
                member __.Untrack _ = ()
            interface IObservable<'a> with
                member __.Subscribe obs = 
                    obs.OnNext(value)
                    obs.OnCompleted()
                    { new IDisposable with
                        member __.Dispose() = ()
                    }
        }

    /// Introduce arbitrary values into a view
    let pure' = constant


    /// <summary>Create a cached view over a provider</summary>
    /// <remarks>
    /// This will not hold a reference to the provider, and will allow it to be garbage collected.
    /// As such, it caches the "last valid" state of the view locally.
    /// </remarks>
    let cache (provider : IView<'a>) = 
        new CachedView<'a>(provider) :> IView<'a>

    /// Create a view from an observable.  As an IView always provides a value, the initial value to use upon creation is required.
    /// Returns view and subscription handle
    let subscribeFromObservable initialValue (observable : IObservable<'a>) =
        let value = Mutable.create initialValue        
        let disposable = observable.Subscribe (fun v -> value.Value <- v)        
        value :> IView<'a> , disposable

    /// Create a subscription to the changes of a view which calls the provided function upon each change
    let subscribe (f : 'a -> unit) (provider : IView<'a>) = 
        let tracker = provider :> ITracksDependents
        let rec dependent =
            {
                new obj() with
                    override this.Finalize() =
                        (this :?> IDisposable).Dispose()
                interface IDependent with
                    member __.RequestRefresh _ =
                        f(provider.Value)
                    member __.HasDependencies with get() = true
                    
                interface IDisposable with
                    member this.Dispose() = 
                        tracker.Untrack dependent
                        GC.SuppressFinalize this
            }
        tracker.Track dependent
        dependent :?> IDisposable
    
    /// Create a subscription to the changes of a view which copies its value upon change into a mutable
    let copyTo (target : IMutatable<'a>) (provider : IView<'a>) =
        target.Value <- provider.Value
        subscribe (fun v -> target.Value <- v) provider

    /// Create a subscription to the changes of a view which copies its value upon change into a mutable via a stepping function
    let copyStep (target : IMutatable<'b>) (stepFunction : 'b -> 'a -> 'b) (provider : IView<'a>) =
        let update() =
            target.Value <- stepFunction target.Value provider.Value
        update()        
        subscribe (fun _ -> update()) provider
        
    /// Gets the current value associated with the view
    let get (view : IView<'a>) = 
        view.Value

    /// Executes a function for a view value.
    let iter (f : 'a -> unit)  (view : IView<'a>)=
        f(view.Value)

    /// Transforms a view value by using a specified mapping function.
    let map (mapping : 'a -> 'b)  (provider : IView<'a>) = 
        let view = new MappingView<'a, 'b>(provider, mapping, false)
        view :> IView<'b>

    /// Transforms two view values by using a specified mapping function.
    let map2 (mapping : 'a -> 'b -> 'c) (provider1 : IView<'a>) (provider2 : IView<'b>) = 
        let view = new Mapping2View<'a, 'b, 'c>(provider1, provider2, mapping)
        view :> IView<'c>

    /// Filters the view, so only values matching the predicate are cached and propogated onwards
    let filter (predicate : 'a -> bool) (provider : IView<'a>) =
        let view = new FilteredView<'a>(provider, predicate, false)
        view :> IView<'a>

    /// Need a description
    let choose (predicate : 'a -> 'b option) (provider : IView<'a>) =        
        let map = new MappingView<'a,'b option>(provider, predicate, false)
        let filter = new FilteredView<'b option>(map, (fun v -> v <> None), true)
        let view = new MappingView<'b option, 'b>(filter, (fun opt -> opt.Value), true)
        view :> IView<'b>

    /// Applies a View of a function in order to provide mapping of arbitrary arity
    let apply (mappingView : IView<'a -> 'b>) provider =        
        let view = new Mapping2View<'a->'b, 'a, 'b>(mappingView, provider, (fun a b -> a b))
        view :> IView<'b>

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

    type internal ValidatorMappingView<'a>(validator : ValidationCollector<'a> -> ValidationCollector<'a>, valueProvider : IView<'a>) as self =
        let dependencies = Dependencies.create [| valueProvider |] self

        let validateCurrent value =
            value
            |> validate 
            |> validator
            |> Validation.result
        let validationResult = 
            valueProvider
            |> map validateCurrent

        override this.Finalize() =
            (this :> IDisposable).Dispose()
            GC.SuppressFinalize this        

        interface IValidatedView<'a> with
            member __.ValidationResult with get() = validationResult

            member __.IsValid = isValid validationResult.Value

        interface IObservable<'a> with
            member __.Subscribe obs = 
                dependencies.Add obs
                { 
                    new IDisposable with
                        member __.Dispose() = dependencies.Remove obs
                }

        interface ITracksDependents with
            member __.Track dep = dependencies.Add dep
            member __.Untrack dep = dependencies.Remove dep

        interface IView<'a> with
            member __.Value with get() = valueProvider.Value

        interface IDependent with
            member this.RequestRefresh _ =             
                dependencies.Signal this
            member __.HasDependencies with get() = dependencies.HasDependencies

        interface IDisposable with
            member this.Dispose() =
                dependencies.RemoveAll()

    /// Validates a view with a validation chain
    let validate<'a> (validator : ValidationCollector<'a> -> ValidationCollector<'a>) (view : IView<'a>) =
        new ValidatorMappingView<'a>(validator, view) :> IValidatedView<'a>

    // Apply in reverse to allow for easy piping in liftN
    let private applyR provider (mappingView : IView<'a -> 'b>) =        
        let view = new Mapping2View<'a->'b, 'a, 'b>(mappingView, provider, (fun a b -> a b))
        view :> IView<'b>
    
    /// Combines two views using a specified function, equivelent to View.map2
    let lift2 f a b = map2 f a b

    let private lift f a b c =
        let f' a bc = f a (fst bc) (snd bc)
        let bc = map2 (fun b c -> b,c) b c
        f', bc
    /// Combines three views using a specified function
    let lift3 f a b c = 
        let f1, bc = lift f a b c
        lift2 f1 a bc
    
    /// Combines four views using a specified function
    let lift4 f a b c d = 
        let f1, bc = lift f a b c
        lift3 f1 a bc d
    
    /// Combines five views using a specified function
    let lift5 f a b c d e = 
        let f1, bc = lift f a b c
        lift4 f1 a bc d e
        
    /// Combines six views using a specified function
    let lift6 f a b c d e f' = 
        let f1, bc = lift f a b c
        lift5 f1 a bc d e f'

    /// Combines seven views using a specified function
    let lift7 f a b c d e f' g= 
        let f1, bc = lift f a b c
        lift6 f1 a bc d e f' g

    /// Combines eight views using a specified function
    let lift8 f a b c d e f' g h = 
        let f1, bc = lift f a b c
        lift7 f1 a bc d e f' g h

    /// Combines nine views using a specified function
    let lift9 f a b c d e f' g h i = 
        let f1, bc = lift f a b c
        lift8 f1 a bc d e f' g h i

    /// Combines ten views using a specified function
    let lift10 f a b c d e f' g h i j = 
        let f1, bc = lift f a b c
        lift9 f1 a bc d e f' g h i j
