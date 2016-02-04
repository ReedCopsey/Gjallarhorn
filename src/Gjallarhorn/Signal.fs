namespace Gjallarhorn

#nowarn "40" "21"

open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System
open System.Collections.Generic

/// Provides mechanisms for working with signals
module Signal =
    
    /// Create a signal over a constant, immutable value
    let constant (value : 'a) = 
        {
            // TODO: Should this use a dependency tracker anyways?  Right now, we always return false on has dependencies, but that's not accurate
            new ISignal<'a> with
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
    
    /// <summary>Create a cached signal over a provider</summary>
    /// <remarks>
    /// This will not hold a reference to the provider, and will allow it to be garbage collected.
    /// As such, it caches the "last valid" state of the signal locally.
    /// </remarks>
    let cache (provider : ISignal<'a>) = 
        new CachedSignal<'a>(provider) :> ISignal<'a>

    module Subscription =
        /// Create a subscription to the changes of a signal which calls the provided function upon each change
        let create (f : 'a -> unit) (provider : ISignal<'a>) = 
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
                }
            tracker.Track dependent
            dependent :?> IDisposable
    
        /// Create a subscription to the changes of a signal which copies its value upon change into a mutable
        let copyTo (target : IMutatable<'a>) (provider : ISignal<'a>) =
            target.Value <- provider.Value
            create (fun v -> target.Value <- v) provider

        /// Create a subscription to the changes of a signal which copies its value upon change into a mutable via a stepping function
        let copyStep (target : IMutatable<'b>) (stepFunction : 'b -> 'a -> 'b) (provider : ISignal<'a>) =
            let update() =
                target.Value <- stepFunction target.Value provider.Value
            update()        
            create (fun _ -> update()) provider

        /// Create a signal from an observable.  As an ISignal always provides a value, the initial value to use upon creation is required.
        /// Returns signal and subscription handle
        let fromObservable initialValue (observable : IObservable<'a>) =
            let value = Mutable.create initialValue        
            let disposable = observable.Subscribe (fun v -> value.Value <- v)        
            value :> ISignal<'a> , disposable

        
    /// Gets the current value associated with the signal
    let get (signal : ISignal<'a>) = 
        signal.Value

    /// Executes a function for a signal value.
    let iter (f : 'a -> unit)  (signal : ISignal<'a>)=
        f(signal.Value)

    /// Transforms a signal value by using a specified mapping function.
    let map (mapping : 'a -> 'b)  (provider : ISignal<'a>) = 
        let signal = new MappingSignal<'a, 'b>(provider, mapping, false)
        signal :> ISignal<'b>

    /// Combines two signals using a specified mapping function
    let map2 (mapping : 'a -> 'b -> 'c) (provider1 : ISignal<'a>) (provider2 : ISignal<'b>) = 
        let signal = new Mapping2Signal<'a, 'b, 'c>(provider1, provider2, mapping)
        signal :> ISignal<'c>

    // Used to do mapN by lifting then mapping
    let private lift f a b c =
        let f' a bc = f a (fst bc) (snd bc)
        let bc = map2 (fun b c -> b,c) b c
        f', bc

    /// Combines three signals using a specified mapping function
    let map3 f v1 v2 v3 = 
        let f1, bc = lift f v1 v2 v3
        map2 f1 v1 bc
    
    /// Combines four signals using a specified mapping function
    let map4 f v1 v2 v3 v4 = 
        let f1, bc = lift f v1 v2 v3
        map3 f1 v1 bc v4
    
    /// Combines five signals using a specified mapping function
    let map5 f v1 v2 v3 v4 v5 = 
        let f1, bc = lift f v1 v2 v3
        map4 f1 v1 bc v4 v5
        
    /// Combines six signals using a specified mapping function
    let map6 f v1 v2 v3 v4 v5 v6 = 
        let f1, bc = lift f v1 v2 v3
        map5 f1 v1 bc v4 v5 v6

    /// Combines seven signals using a specified mapping function
    let map7 f v1 v2 v3 v4 v5 v6 v7= 
        let f1, bc = lift f v1 v2 v3
        map6 f1 v1 bc v4 v5 v6 v7

    /// Combines eight signals using a specified mapping function
    let map8 f v1 v2 v3 v4 v5 v6 v7 v8 = 
        let f1, bc = lift f v1 v2 v3
        map7 f1 v1 bc v4 v5 v6 v7 v8

    /// Combines nine signals using a specified mapping function
    let map9 f v1 v2 v3 v4 v5 v6 v7 v8 v9 = 
        let f1, bc = lift f v1 v2 v3
        map8 f1 v1 bc v4 v5 v6 v7 v8 v9

    /// Combines ten signals using a specified mapping function
    let map10 f v1 v2 v3 v4 v5 v6 v7 v8 v9 v10 = 
        let f1, bc = lift f v1 v2 v3
        map9 f1 v1 bc v4 v5 v6 v7 v8 v9 v10

    /// Filters the signal, so only values matching the predicate are cached and propogated onwards
    let filter (predicate : 'a -> bool) (provider : ISignal<'a>) =
        let signal = new FilteredSignalToObservable<'a>(provider, predicate, false)
        signal :> IObservable<'a>

    /// Filters the signal by using a separate bool signal
    let filterBy condition input =
        new IfSignal<_>(input, condition) :> IObservable<_>

    /// Need a description
    let choose (predicate : 'a -> 'b option) (provider : ISignal<'a>) =        
        let map = new MappingSignal<'a,'b option>(provider, predicate, false)
        let filter = new FilteredSignalToObservable<'b option>(map, (fun v -> not(v.IsNone)), true) :> IObservable<'b option>
        filter 
        |> Observable.map (fun opt -> opt.Value)

    /// Combines two signals into a single signal.  The value from the second signal is used as the initial value of the result
    let combine a b =
        new CombineSignal<_>(a, b) :> ISignal<_>

    /// Creates a signal on two values that is true if both inputs are equal
    let equal a b =
        map2 (fun a b -> a = b) a b

    /// Creates a signal on two values that is true if both inputs are not equal
    let notEqual a b =
        map2 (fun a b -> a <> b) a b
       
    /// Creates a signal over a bool value that negates the input
    let not a =
        map (fun a -> not(a)) a
    
    /// Creates a signal on two bools that is true if both inputs are true
    let both (a : ISignal<bool>) (b : ISignal<bool>) =
        map2 (fun a b -> a && b) a b

    /// Creates a signal on two bools that is true if either input is true
    let either (a : ISignal<bool>) (b : ISignal<bool>) =
        map2 (fun a b -> a || b) a b

    /// Creates a signal that schedules on a synchronization context
    let observeOn ctx (signal : ISignal<'a>) =
        new ObserveOnSignal<'a>(signal, ctx) :> ISignal<'a>
                
    type internal ValidatorMappingSignal<'a>(validator : ValidationCollector<'a> -> ValidationCollector<'a>, valueProvider : ISignal<'a>) as self =
        let dependencies = Dependencies.create [| valueProvider |] self
        let validationDeps = Dependencies.create [| valueProvider |] (constant ValidationResult.Valid)
        let mutable valueProvider = Some(valueProvider)

        let validateCurrent value =
            value
            |> validate 
            |> validator
            |> Validation.result

        let mutable lastValue = valueProvider.Value.Value
        let mutable lastVaidation = validateCurrent lastValue


        member private this.Signal signalValidation signalValue = 
            if signalValidation then validationDeps.Signal (this :> IValidatedSignal<'a>).ValidationResult
            if signalValue then dependencies.Signal this

        member private this.UpdateAndGetValue () =
            let value () = DisposeHelpers.getValue valueProvider (fun _ -> self.GetType().FullName)
            let value = value()
            let signalValue, signalValidation =
                if false = EqualityComparer<_>.Default.Equals(lastValue, value) then
                    lastValue <- value
                    
                    let valid = validateCurrent value
                    let signalV = valid <> lastVaidation
                    lastVaidation <- valid
                    true, signalV
                else 
                    false, false            
            this.Signal signalValidation signalValue

            lastValue

        override this.Finalize() =
            (this :> IDisposable).Dispose()            

        interface IObservable<'a> with
            member this.Subscribe (obs : IObserver<'a>) : IDisposable = 
                dependencies.Add (obs,this)
                { 
                    new IDisposable with
                        member __.Dispose() = dependencies.Remove (obs,this)
                }

        interface IValidatedSignal<'a> with
            member this.ValidationResult 
                with get() = 
                    let rec result =
                        { 
                            new ISignal<ValidationResult> with
                                member __.Value 
                                    with get() = validateCurrent (self.UpdateAndGetValue())
                            interface IObservable<ValidationResult> with
                                member __.Subscribe obs = 
                                    validationDeps.Add (obs,result)
                                    {
                                        new IDisposable with
                                            member __.Dispose() = validationDeps.Remove (obs,result)
                                    }

                            interface IDependent with
                                member __.RequestRefresh _ = this.UpdateAndGetValue() |> ignore
                                member __.HasDependencies = dependencies.HasDependencies || validationDeps.HasDependencies
                            interface ITracksDependents with
                                member __.Track dep = validationDeps.Add (dep,result)
                                member __.Untrack dep = validationDeps.Remove (dep,result)
                        }
                    result

            member this.IsValid 
                with get() = isValid (validateCurrent (this.UpdateAndGetValue()))

        interface ITracksDependents with
            member this.Track dep = dependencies.Add (dep,this)
            member this.Untrack dep = dependencies.Remove (dep,this)

        interface ISignal<'a> with
            member this.Value 
                with get() = this.UpdateAndGetValue()                    

        interface IDependent with
            member this.RequestRefresh _ = this.UpdateAndGetValue() |> ignore
            member __.HasDependencies with get() = dependencies.HasDependencies || validationDeps.HasDependencies

        interface IDisposable with
            member this.Dispose() =
                DisposeHelpers.dispose valueProvider false this
                valueProvider <- None
                validationDeps.RemoveAll (this :> IValidatedSignal<'a>).ValidationResult
                dependencies.RemoveAll this
                GC.SuppressFinalize this

    /// Validates a signal with a validation chain
    let validate<'a> (validator : ValidationCollector<'a> -> ValidationCollector<'a>) (signal : ISignal<'a>) =
        new ValidatorMappingSignal<'a>(validator, signal) :> IValidatedSignal<'a>    