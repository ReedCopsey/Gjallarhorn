namespace Gjallarhorn

open System
open Gjallarhorn.Internal


// The messages we allow for manipulation of our state
type private PostMessage<'TModel,'TMsg> =
    | Get of AsyncReplyChannel<'TModel>                                  
    | Set of 'TModel * AsyncReplyChannel<'TModel>
    | Update of 'TMsg * AsyncReplyChannel<'TModel>

/// Type which manages state internally given an initial state and an update function
type State<'TModel,'TMsg> (initialState : 'TModel, update : 'TMsg -> 'TModel -> 'TModel) =
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
                | Update(msg, replyChannel) ->
                    let state = 
                        update msg current
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
    member __.Update updateRequest = stateManager.PostAndReply (fun c -> Update(updateRequest, c))
    /// Perform an update on the current state asynchronously
    member __.UpdateAsync updateRequest = stateManager.PostAndAsyncReply (fun c -> Update(updateRequest, c))    

    interface IObservable<'TModel> with
        member __.Subscribe obs = (signal :> IObservable<_>).Subscribe obs
    interface ITracksDependents with
        member __.Track dep = (signal :> ITracksDependents).Track dep 
        member __.Untrack dep = (signal :> ITracksDependents).Untrack dep 
    interface IDependent with
        member __.UpdateDirtyFlag v = (signal :> IDependent).UpdateDirtyFlag v
        member __.HasDependencies with get() = (signal :> IDependent).HasDependencies
    interface ISignal<'TModel> with
        member __.Value with get() = signal.Value

    interface IMutatable<'TModel> with
        member this.Value with get() = signal.Value and set(v) = this.Set(v) |> ignore
        
    interface System.IDisposable with
        member this.Dispose() = 
            (signal :?> System.IDisposable).Dispose()            
            GC.SuppressFinalize this
