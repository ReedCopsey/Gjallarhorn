namespace Gjallarhorn

open System
open System.Threading
open Gjallarhorn.Internal

// The messages we allow for manipulation of our state
[<NoComparison;NoEquality>]
type private PostMessage<'a> =
    | Get of AsyncReplyChannel<'a>                                  
    | Set of 'a * AsyncReplyChannel<'a>
    | Update of ('a -> 'a) * AsyncReplyChannel<'a>

/// Type which manages state internally using a mailbox
type AsyncMutable<'a> (initialState : 'a) =
    // Provide a mechanism to publish changes to our state as an observable
    // Note that we could have used a mutable here, but that would effectively
    // "duplicate state"
    let stateChangedEvent = Event<_>()

    let signal = Signal.fromObservable initialState stateChangedEvent.Publish    

    // Manage our state internally using a mailbox processor
    // This lets us post updates from any thread
    let stateManager = 
        let notifyStateUpdated state = 
            // Trigger our new state has changed
            stateChangedEvent.Trigger state
            state

        MailboxProcessor.Start(fun inbox ->
            let rec loop current = async {
                let! msg = inbox.Receive()

                match msg with 
                | Get replyChannel -> 
                    replyChannel.Reply current
                    return! loop current
                | Set (newState, replyChannel) -> 
                    let state = newState |> notifyStateUpdated
                    replyChannel.Reply state
                    return! loop state
                | Update(fn, replyChannel) ->
                    let state = 
                        fn current
                        |> notifyStateUpdated
                    replyChannel.Reply state
                    return! loop state
            }
                                    
            loop initialState )

    /// Get the current state synchronously
    member __.Get () = Get |> stateManager.PostAndReply 
    /// Get the current state asynchronously
    member __.GetAsync () = Get |> stateManager.PostAndAsyncReply

    /// Set the state to a new value synchronously
    member __.Set model = stateManager.PostAndReply (fun c -> Set(model, c))
    
    /// Set the state to a new value asynchronously
    member __.SetAsync model = stateManager.PostAndAsyncReply (fun c -> Set(model, c))

    /// Perform an update on the current state
    member __.Update fn = stateManager.PostAndReply (fun c -> Update(fn, c))
    /// Perform an update on the current state asynchronously
    member __.UpdateAsync fn = stateManager.PostAndAsyncReply (fun c -> Update(fn, c))    

    interface IObservable<'a> with
        member __.Subscribe obs = (signal :> IObservable<_>).Subscribe obs
    interface ITracksDependents with
        member __.Track dep = (signal :> ITracksDependents).Track dep 
        member __.Untrack dep = (signal :> ITracksDependents).Untrack dep 
    interface IDependent with
        member __.UpdateDirtyFlag v = (signal :> IDependent).UpdateDirtyFlag v
        member __.HasDependencies with get() = (signal :> IDependent).HasDependencies
    interface ISignal<'a> with
        member __.Value with get() = signal.Value

    interface IMutatable<'a> with
        member this.Value with get() = signal.Value and set(v) = this.Set(v) |> ignore

    interface IAtomicMutatable<'a> with
        member this.Update f = this.Update f

    interface IAsyncMutatable<'a> with
        member this.UpdateAsync fn = this.UpdateAsync fn
        member this.GetAsync () = this.GetAsync ()
        member this.SetAsync v = this.SetAsync v
        
    interface System.IDisposable with
        member this.Dispose() = 
            (signal :?> System.IDisposable).Dispose()            
            GC.SuppressFinalize this

/// A thread-safe wrapper using interlock for a mutable value with change notification
type AtomicMutable<'a when 'a : not struct>(value : 'a) as self =
    let mutable v = value
    let deps = Dependencies.create [||] self
    let swap (f : 'a -> 'a) =
        let sw = SpinWait()        
        let mutable current = v
        while not ( obj.ReferenceEquals(Interlocked.CompareExchange<'a>(&v, f current, current), current) ) do
            sw.SpinOnce()            
            current <- v
        deps.MarkDirty self
        v

    let setValue value =
        Interlocked.Exchange<'a>(&v, value) |> ignore
        deps.MarkDirty self

    /// Gets and sets the Value contained within this mutable
    member __.Value 
        with get() = v
        and set(value) = setValue value

    /// Updates the current value in a manner that guarantees proper execution, 
    /// given a function that takes the current value and generates a new value,
    /// and then returns the new value
    /// <remarks>The function may be executed multiple times, depending on the implementation.</remarks>
    member __.Update f = swap f

    interface IAtomicMutatable<'a> with
        member this.Update f = this.Update f
            
    interface System.IDisposable with
        member this.Dispose() =
            deps.RemoveAll this
            GC.SuppressFinalize this
    interface IObservable<'a> with
        member this.Subscribe obs = deps.Subscribe(obs, this)
    interface ITracksDependents with
        member this.Track dep = deps.Add (dep, this)
        member this.Untrack dep = deps.Remove (dep, this)
    interface IDependent with
        member __.UpdateDirtyFlag _ = ()
        member __.HasDependencies with get() = deps.HasDependencies
    interface ISignal<'a> with
        member __.Value with get() = v
    interface IMutatable<'a> with
        member __.Value with get() = v and set(v) = setValue v

