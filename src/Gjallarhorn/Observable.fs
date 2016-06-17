namespace Gjallarhorn

open Gjallarhorn.Helpers
open Gjallarhorn.Internal
open System

/// Additional functions related to Observable for use with Gjallarhorn
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Observable =
    /// Filters the input observable by using a separate bool signal. The value of the signal is used as the filtering predicate
    let filterBy (condition : ISignal<bool>) input =
        input
        |> Observable.filter (fun _ -> condition.Value)

    /// Filters out observables of options to only pass through
    /// "Some" values
    let filterSome (provider: IObservable<'a option>) =
        provider
        |> Observable.choose id

    /// Maps the input observable through an async workflow.
    let mapAsync (mapping : 'a -> Async<'b>) (provider : IObservable<'a>) =
        let evt = Event<_>()
        let callback value =
            async {
                let! asyncResult = mapping value    
                evt.Trigger asyncResult
            } 
            |> Async.Start
            
        let subscription = provider.Subscribe callback
        {
            new IObservable<'b> with
                member __.Subscribe obs =
                    let disp = new CompositeDisposable()
                    disp.Add subscription
                    (evt.Publish :> IObservable<'b>).Subscribe obs |> disp.Add
                    disp :> IDisposable
        }

    /// Maps the input observable through an async workflow. Execution status is reported through the specified IdleTracker.
    let mapAsyncTracked (mapping : 'a -> Async<'b>) (tracker : IdleTracker) (provider : IObservable<'a>) =
        let trackingMap v =
            async {
                use _execHandle = tracker.GetExecutionHandle()
                let! result = mapping v
                return result               
            }
        mapAsync trackingMap provider

    module Subscription =
        /// Create a subscription to an observable which copies its value upon change into a mutable
        let copyTo (target : IMutatable<'a>) (provider : IObservable<'a>) =            
            provider.Subscribe(fun v -> target.Value <- v)