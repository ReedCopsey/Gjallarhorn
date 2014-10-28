namespace Gjallarhorn.Control

open Gjallarhorn
open Gjallarhorn.Internal

open System

/// Provides mechanisms for working with IView views
module View =

    (**************************************************
     "Creation" functions for building IView
    ***************************************************)
    
    /// Create a view over a constant, immutable value
    let constant (value : 'a) = 
        {
            new IView<'a> with
                member __.Value = value
        }

    /// <summary>Create a cached view over a provider</summary>
    /// <remarks>
    /// This will not hold a reference to the provider, and will allow it to be garbage collected.
    /// As such, it caches the "last valid" state of the view locally.
    /// </remarks>
    let cache (provider : IView<'a>) = new ViewCache<'a>(provider) :> IDisposableView<'a>

    /// Create a view from an observable.  As an IView always provides a value, the initial value to use upon creation is required     
    let fromObservable initialValue (observable : IObservable<'a>) =
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

    (**************************************************
     "Subscription" functions for tracking IView changes
     without using a new IView
    ***************************************************)

    /// Add a permanent subscription to the changes of a view which calls the provided function upon each change
    let add (f : 'a -> unit) (provider : IView<'a>) = 
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
    let subscribe (f : 'a -> unit) (provider : IView<'a>) = 
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
    
    (**************************************************
     "Composition" functions for operating with IView
    ***************************************************)
    /// Gets the current value associated with the view
    let get (view : IView<'a>) = 
        view.Value

    /// Executes a function for a view value.
    let iter (f : 'a -> unit)  (view : IView<'a>)=
        f(view.Value)

    /// Transforms a view value by using a specified mapping function.
    let map (mapping : 'a -> 'b)  (provider : IView<'a>) = 
        let view = new View<'a, 'b>(provider, mapping)
        view :> IDisposableView<'b>

    /// Transforms two view values by using a specified mapping function.
    let map2 (mapping : 'a -> 'b -> 'c) (provider1 : IView<'a>) (provider2 : IView<'b>) = 
        let view = new View2<'a, 'b, 'c>(provider1, provider2, mapping)
        view :> IDisposableView<'c>

    let apply (mappingView : IView<'a -> 'b>) provider =        
        let view = new View2<'a->'b, 'a, 'b>(mappingView, provider, (fun a b -> a b))
        view :> IDisposableView<'b>

    /// <summary>Converts any IView into an IObservable</summary>
    /// <remarks>The result can be Disposed to stop tracking</remarks>
    let asObservable (view : IView<'a>) =
        new Observer<'a>(view) :> IDisposableObservable<'a>


[<AutoOpen>]
module ViewOperators =
    let ( <*> ) (f : IView<'a->'b>) (x : IView<'a>) : IView<'b> = View.apply f x :> IView<'b>