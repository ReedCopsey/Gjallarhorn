namespace Gjallarhorn.Linq

open Gjallarhorn
open Gjallarhorn.Helpers
open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System
open System.ComponentModel
open System.Runtime.CompilerServices
open System.Threading.Tasks

/// Functions for working with Mutables from C#
[<AbstractClass;Sealed>]
type Mutable() =
    /// Create a mutable given an initial value
    static member Create<'a> (value : 'a) =
        Mutable<'a>(value) :> IMutatable<'a>
    
    /// Update a mutable given a stepping function
    static member Update<'a> (original : IMutatable<'a>, steppingFunction : Func<'a, 'a>) =
        Mutable.step steppingFunction.Invoke original

/// Functions for working with Signals from C#
[<AbstractClass;Sealed>]
type Signal() =
    /// Create a signal (which never notifies) given a constant value
    static member FromConstant<'a> (value : 'a) =
        Signal.constant value

    /// Combines signals using a specified mapping function
    static member Combine (signal1, signal2, mapping:Func<_,_,_>) =
        Signal.map2 (mapping.ToFSharpFunc()) signal1 signal2

    /// Combines signals using a specified mapping function
    static member Combine (signal1, signal2, signal3, mapping:Func<_,_,_,_>) =
        Signal.map3 (mapping.ToFSharpFunc()) signal1 signal2 signal3

    /// Combines signals using a specified mapping function
    static member Combine (signal1, signal2, signal3, signal4, mapping:Func<_,_,_,_,_>) =
        Signal.map4 (mapping.ToFSharpFunc()) signal1 signal2 signal3 signal4

    /// Combines signals using a specified mapping function
    static member Combine (signal1, signal2, signal3, signal4, signal5, mapping:Func<_,_,_,_,_,_>) =
        Signal.map5 (mapping.ToFSharpFunc()) signal1 signal2 signal3 signal4 signal5

    /// Combines signals using a specified mapping function
    static member Combine (signal1, signal2, signal3, signal4, signal5, signal6, mapping:Func<_,_,_,_,_,_,_>) =
        Signal.map6 (mapping.ToFSharpFunc()) signal1 signal2 signal3 signal4 signal5 signal6

    /// Combines signals using a specified mapping function
    static member Combine (signal1, signal2, signal3, signal4, signal5, signal6, signal7, mapping:Func<_,_,_,_,_,_,_,_>) =
        Signal.map7 (mapping.ToFSharpFunc()) signal1 signal2 signal3 signal4 signal5 signal6 signal7

    /// Combines signals using a specified mapping function
    static member Combine (signal1, signal2, signal3, signal4, signal5, signal6, signal7, signal8, mapping:Func<_,_,_,_,_,_,_,_,_>) =
        Signal.map8 (mapping.ToFSharpFunc()) signal1 signal2 signal3 signal4 signal5 signal6 signal7 signal8

    /// Combines signals using a specified mapping function
    static member Combine (signal1, signal2, signal3, signal4, signal5, signal6, signal7, signal8, signal9, mapping:Func<_,_,_,_,_,_,_,_,_,_>) =
        Signal.map9 (mapping.ToFSharpFunc()) signal1 signal2 signal3 signal4 signal5 signal6 signal7 signal8 signal9

    /// Combines signals using a specified mapping function
    static member Combine (signal1, signal2, signal3, signal4, signal5, signal6, signal7, signal8, signal9, signal10, mapping:Func<_,_,_,_,_,_,_,_,_,_,_>) =
        Signal.map10 (mapping.ToFSharpFunc()) signal1 signal2 signal3 signal4 signal5 signal6 signal7 signal8 signal9 signal10

/// Extension methods for working with Signals from C# using a LINQ inspired API    
[<AbstractClass;Sealed;Extension>]
type SignalExtensions() =
    /// Create a cached signal over a provider
    /// <remarks>
    /// This will not hold a reference to the provider, and will allow it to be garbage collected.
    /// As such, it caches the "last valid" state of the signal locally.
    /// </remarks>
    [<Extension>]
    static member Cached<'a>(this : ISignal<'a>) =
        Signal.cache this

    /// Create a subscription to the changes of a signal which calls the provided function upon each change
    [<Extension>]
    static member Subscribe<'a>(this : ISignal<'a>, func : Action<'a>) =
        Signal.Subscription.create func.Invoke this

    /// Create a subscription to the changes of a signal which copies its value upon change into a mutable
    [<Extension>]
    static member CopyTo<'a>(this : ISignal<'a>, target : IMutatable<'a>) =
        Signal.Subscription.copyTo target this

    [<Extension>]
    /// Create a subscription to the changes of a signal which copies its value upon change into a mutable via a stepping function
    static member SubscribeAndUpdate<'a,'b>(this : ISignal<'a>, target : IMutatable<'b>, stepFunction : Func<'b,'a,'b>) =
        let f = stepFunction.ToFSharpFunc()
        Signal.Subscription.copyStep target f this

    // TODO: port fromObservable

    [<Extension>]
    /// Perform a mapping from one signal to another
    static member Select<'a,'b> (this:ISignal<'a>, mapper:Func<'a,'b>) =
        this
        |> Signal.map (mapper.ToFSharpFunc())

    [<Extension>]
    /// Perform an asynchronous mapping from one signal to another
    static member SelectAsync<'a,'b> (this:ISignal<'a>, initialValue:'b, mapper:Func<'a,Task<'b>>) =
        let mapping a = 
            async {
                let! result = Async.AwaitTask (mapper.Invoke a)
                return result
            }
        this
        |> Signal.mapAsync mapping initialValue

    [<Extension>]
    /// Perform an asynchronous mapping from one signal to another, tracking execution via an IdleTracker
    static member SelectAsync<'a,'b> (this:ISignal<'a>, initialValue:'b, tracker, mapper:Func<'a,Task<'b>>) =
        let mapping a = 
            async {
                let! result = Async.AwaitTask (mapper.Invoke a)
                return result
            }
        this
        |> Signal.mapAsyncTracked mapping initialValue tracker
                
    [<Extension;EditorBrowsable(EditorBrowsableState.Never)>]
    /// Perform a projection from a signal, typically only used for query syntax
    static member SelectMany<'a,'b>(this:ISignal<'a>, mapper:Func<'a,ISignal<'b>>) =
        mapper.Invoke(this.Value)

    [<Extension;EditorBrowsable(EditorBrowsableState.Never)>]
    /// Perform a projection from a signal, typically only used for query syntax
    static member SelectMany<'a,'b,'c>(this:ISignal<'a>, mapper:Func<'a,ISignal<'b>>, selector:Func<'a,'b,'c>) : ISignal<'c> =
        let b' = mapper.Invoke(this.Value)
        Signal.map2 (fun a b -> selector.Invoke(a,b)) this b'

    [<Extension>]
    /// Perform a filter from one signal to another based on a predicate.
    /// This will raise an exception if the input value does not match the predicate when created.
    static member Where<'a> (this:ISignal<'a>, filter:Func<'a,bool>) =
        this
        |> Signal.filter (filter.ToFSharpFunc()) this.Value

    [<Extension>]
    /// Perform a filter from one signal to another based on a predicate.
    /// The defaultValue is used to initialize the output signal if the input doesn't match the predicate
    static member Where<'a> (this:ISignal<'a>, filter:Func<'a,bool>, defaultValue) =
        this
        |> Signal.filter (filter.ToFSharpFunc()) defaultValue

/// Extension methods for working with Observables from C# using a LINQ inspired API    
[<AbstractClass;Sealed;Extension>]
type ObservableExtensions() =
    [<Extension>]
    /// Convert from an observable and an initial value to a signal
    static member ToSignal<'a>(this:IObservable<'a>, initialValue:'a) =
        Signal.fromObservable initialValue this