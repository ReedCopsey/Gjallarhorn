namespace Gjallarhorn

#nowarn "40" "21"

open Gjallarhorn.Helpers
open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System
open System.Collections.Generic

/// Provides mechanisms for working with signals
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Signal =
    
    /// Create a signal over a constant, immutable value
    let constant (value : 'a) = 
        {
            // TODO: Should this use a dependency tracker anyways?  Right now, we always return false on has dependencies, but that's not accurate
            new ISignal<'a> with
                member __.Value = value
            interface IDependent with
                member __.UpdateDirtyFlag _ = ()
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
        CachedSignal.Create<'a>(provider) :> ISignal<'a>

    module Subscription =
        /// Create a subscription to the changes of a signal which calls the provided function upon each change
        let create (f : 'a -> unit) (provider : ISignal<'a>) = 
            let tracker = provider

            let mutable lastValue = provider.Value

            let conditionallyUpdate () =
                let v = provider.Value
                if lastValue <> v then
                    lastValue <- v
                    f(v)

            let rec dependent =
                {
                    new obj() with
                        override this.Finalize() =
                            (this :?> IDisposable).Dispose()
                    interface IDependent with
                        member __.UpdateDirtyFlag _ =
                            // Subscriptions force a recompute when dirty
                            conditionallyUpdate () 
                        member __.HasDependencies with get() = true
                    
                    interface IDisposable with
                        member __.Dispose() = 
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
    let fromObservable initialValue (observable : IObservable<'a>) =
        ObservableToSignal.Create<'a>(observable, initialValue) :> ISignal<'a> 
        
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

    /// Transforms a signal value asynchronously by using a specified mapping function.
    let mapAsync<'a,'b when 'b : equality> (mapping : 'a -> Async<'b>) initialValue (provider : ISignal<'a>) =
        let signal = AsyncMappingSignal.Create<'a,'b>(provider,initialValue, None, mapping)
        signal :> ISignal<'b>

    /// Transforms a signal value asynchronously by using a specified mapping function. Execution status is reported through the specified IdleTracker
    let mapAsyncTracked (mapping : 'a -> Async<'b>) initialValue tracker (provider : ISignal<'a>) =
        let signal = AsyncMappingSignal.Create<'a,'b>(provider, initialValue, Some(tracker), mapping)
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

    // Like Option.map for 2 options
    let private optionMap2 mapping a' b' =
        match a', b' with
        | Some a, Some b -> Some (mapping a b)
        | _ -> None
    
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

    /// Combines two signals of options, such as validation results, 
    /// using a specified mapping function
    /// If either is None, the result is None
    let mapOption2 (mapping : 'a -> 'b -> 'c) (provider1 : ISignal<'a option>) (provider2 : ISignal<'b option>) = 
        map2 (optionMap2 mapping) provider1 provider2

    // Lift function for options
    let private liftO f a b c =
        let f' a bc = f a (fst bc) (snd bc)
        let bc = mapOption2 (fun b c -> b,c) b c
        f', bc

    /// Combines three signals of options using a specified mapping function
    let mapOption3 f v1 v2 v3 = 
        let f1, bc = liftO f v1 v2 v3
        mapOption2 f1 v1 bc
    
    /// Combines four signals of options using a specified mapping function
    let mapOption4 f v1 v2 v3 v4 = 
        let f1, bc = liftO f v1 v2 v3
        mapOption3 f1 v1 bc v4

    /// Combines five signals of options using a specified mapping function
    let mapOption5 f v1 v2 v3 v4 v5 = 
        let f1, bc = liftO f v1 v2 v3
        mapOption4 f1 v1 bc v4 v5
        
    /// Combines six signals of options using a specified mapping function
    let mapOption6 f v1 v2 v3 v4 v5 v6 = 
        let f1, bc = liftO f v1 v2 v3
        mapOption5 f1 v1 bc v4 v5 v6

    /// Combines seven signals of options using a specified mapping function
    let mapOption7 f v1 v2 v3 v4 v5 v6 v7= 
        let f1, bc = liftO f v1 v2 v3
        mapOption6 f1 v1 bc v4 v5 v6 v7

    /// Combines eight signals of options using a specified mapping function
    let mapOption8 f v1 v2 v3 v4 v5 v6 v7 v8 = 
        let f1, bc = liftO f v1 v2 v3
        mapOption7 f1 v1 bc v4 v5 v6 v7 v8

    /// Combines nine signals of options using a specified mapping function
    let mapOption9 f v1 v2 v3 v4 v5 v6 v7 v8 v9 = 
        let f1, bc = liftO f v1 v2 v3
        mapOption8 f1 v1 bc v4 v5 v6 v7 v8 v9

    /// Combines ten signals of options using a specified mapping function
    let mapOption10 f v1 v2 v3 v4 v5 v6 v7 v8 v9 v10 = 
        let f1, bc = liftO f v1 v2 v3
        mapOption9 f1 v1 bc v4 v5 v6 v7 v8 v9 v10

    /// Filters the signal, so only values matching the predicate are cached and propogated onwards. 
    /// If the provider's value doesn't match the predicate, the resulting signal begins with the provided defaultValue.
    let filter (predicate : 'a -> bool) defaultValue (provider : ISignal<'a>) =
        match predicate(defaultValue) with
        | true ->
            let signal = new FilteredSignal<'a>(provider, defaultValue, predicate, false)
            signal :> ISignal<'a>
        | false ->
            let exMsg = sprintf "predicate(%A) returns false. The provided defaultValue must return true with the provided predicate." defaultValue
            raise <| ArgumentException(exMsg, "defaultValue")

    /// Filters the signal by using a separate bool signal
    /// If the condition's Value is initially false, the resulting signal begins with the provided defaultValue.
    let filterBy condition defaultValue input =
        new IfSignal<_>(input, defaultValue, condition) :> ISignal<_>

    /// Returns a signal which is the projection of the input signal using the given function. All observations which return Some
    /// get mapped into the new value.  The defaultValue is used if the input signal's value returns None in the projection
    let choose (fn : 'a -> 'b option) (defaultValue : 'b) (provider : ISignal<'a>) =        
        new ChooseSignal<'a,'b>(provider, defaultValue, fn) :> ISignal<'b>        

    /// Merges two signals into a single signal.  The value from the second signal is used as the initial value of the result
    let merge a b =
        new MergeSignal<_>(a, b) :> ISignal<_>

    /// Creates a signal on two values that is true if both inputs are equal
    let equal a b =
        map2 (=) a b

    /// Creates a signal on two values that is true if both inputs are not equal
    let notEqual a b =
        map2 (<>) a b
       
    /// Creates a signal over a bool value that negates the input
    let not a =
        map (fun a -> not(a)) a
    
    /// Creates a signal on two bools that is true if both inputs are true
    let both (a : ISignal<bool>) (b : ISignal<bool>) =
        map2 (&&) a b

    /// Creates a signal on two bools that is true if either input is true
    let either (a : ISignal<bool>) (b : ISignal<bool>) =
        map2 (||) a b

    /// Creates a signal that schedules on a synchronization context
    let observeOn ctx (signal : ISignal<'a>) =
        new ObserveOnSignal<'a>(signal, ctx) :> ISignal<'a>
                
    type internal ValidatorMappingSignal<'a,'b when 'a : equality>(validator : ValidationCollector<'a> -> ValidationCollector<'b>, valueProvider : ISignal<'a>) as self =
        inherit SignalBase<'b option>([| valueProvider |])
        let validationDeps = Dependencies.create [| valueProvider |] (constant ValidationResult.Valid)

        let rawInput = valueProvider

        let validateCurrent value =
            let validator = 
                value
                |> validate 
                |> validator
            match validator with
            | ValidationCollector.Valid(v) -> v, validator |> Validation.result
            | ValidationCollector.Invalid(v,_,_) -> v, validator |> Validation.result
            
        let mutable lastValidation = validateCurrent (valueProvider.Value)        
        
        let mutable lastInputValue = valueProvider.Value
        let mutable lastValue = 
            match lastValidation with
            | v, ValidationResult.Valid -> Some v
            | _, ValidationResult.Invalid(_) -> None

        let mutable valueProvider = Some(valueProvider)

        override __.HasDependencies 
            with get() =
                base.HasDependencies || validationDeps.HasDependencies

        // This requires custom signaling
        member private this.ValidateSignal signalValidation signalValue source = 
            if signalValidation then 
                validationDeps.MarkDirty (this :> IValidatedSignal<'a,'b>).ValidationResult
            if signalValue then 
                base.MarkDirty source

        member private this.Update source =            
            let value = DisposeHelpers.getValue valueProvider (fun _ -> this.GetType().FullName)
            let signalValue, signalValidation =
                if lastInputValue <> value then
                    lastInputValue <- value
                    let valid = validateCurrent value
                    lastValue <- if (snd valid).IsValidResult then Some (fst valid) else None
                    let signalV = (snd valid) <> (snd lastValidation)
                    lastValidation <- valid
                    true, signalV
                else 
                    false, false            
            this.ValidateSignal signalValidation signalValue source

        override __.UpdateAndGetCurrentValue _ = lastValue

        override this.MarkDirty source = this.Update source

        override this.OnDisposing () =
            DisposeHelpers.cleanup &valueProvider false this
            validationDeps.RemoveAll (this :> IValidatedSignal<'a,'b>).ValidationResult

        interface IValidatedSignal<'a, 'b> with
            member __.RawInput = rawInput
            member this.ValidationResult 
                with get() = 
                    let rec result =
                        { 
                            new ISignal<ValidationResult> with
                                member __.Value 
                                    with get() = 
                                        self.Update()
                                        snd lastValidation
                            interface IObservable<ValidationResult> with
                                member __.Subscribe obs = validationDeps.Subscribe (obs,result)
                            interface IDependent with
                                member __.UpdateDirtyFlag _ = this.Update()
                                member __.HasDependencies = this.HasDependencies
                            interface ITracksDependents with
                                member __.Track dep = validationDeps.Add (dep,result)
                                member __.Untrack dep = validationDeps.Remove (dep,result)
                        }
                    result

            member this.IsValid 
                with get() = 
                    this.Update()
                    isValid (snd lastValidation)

    /// Validates a signal with a validation chain
    let validate<'a,'b when 'a : equality> (validator : ValidationCollector<'a> -> ValidationCollector<'b>) (signal : ISignal<'a>) =
        // TODO: Should we pass through defaults?
        new ValidatorMappingSignal<'a,'b>(validator, signal) :> IValidatedSignal<'a, 'b>    