namespace CollectionSample

open System
open CollectionSample.Model

// This module manages the internal state, instead of letting the basic framework 
// do it.
module internal State =   

    // The messages we allow for manipulation of our state
    type PostMessage =
        // Get the current state
        | Get of AsyncReplyChannel<Requests>                                  
        // Update based on an UpdateRequest
        | Update of Operations.Update                                             
        // Process all Accepted or Rejected items that were updated at least "minimumLife" ago
        // and return the requests that were purged from the model
        | Process of AsyncReplyChannel<Requests> * minimumLife : TimeSpan     
    
    // Provide a mechanism to publish changes to our state as an observable
    // Note that we could have used a mutable here, but that would effectively
    // "duplicate state"
    let private stateChangedEvent = Event<_>()

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
                | Update(msg) ->
                    let state = 
                        Operations.update msg current
                        |> notifyStateUpdated
                    return! loop state
                | Process(replyChannel, life) ->
                    let newState, discards = current |> Operations.partitionProcessed life
                    let state = newState |> notifyStateUpdated
                    replyChannel.Reply discards
                    return! loop state
            }
                                    
            loop [] )

    // Publish our event of changing states
    let stateChanged = stateChangedEvent.Publish
