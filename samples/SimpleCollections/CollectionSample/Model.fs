namespace CollectionSample

open System
open Gjallarhorn

// Note that this program is defined in a PCL, and is completely platform neutral.
// It will work unchanged on WPF, Xamarin Forms, etc

// This is definitely on the "complicated" side, but shows how to use multiple 
// mailbox processors, defined outside of the main UI, to manage a model
// defined itself as a collection

// *********************************************************************************
// The basic model types
type Status =
    | Unknown
    | Accepted
    | Rejected

type Request = { Person : string ; ExpectedHours : float ; Status : Status}    

type Requests = Request list

// These are the updates that can be performed on our requests
type UpdateRequest = 
    | Accept of Request
    | Reject of Request

// *********************************************************************************

// This module manages the internal state, instead of letting the basic framework 
// do it.
module internal State =   

    // The messages we allow
    type PostMessage =
        | Get of AsyncReplyChannel<Requests>    // Get the current state
        | Update of UpdateRequest               // Update based on an UpdateRequest
        | PurgeHandled                          // Purge all Accepted or Rejected items from the model
        | AddNew of string * float              // Add a new request
    
    // Provide a mechanism to publish changes to our state as an observable
    // Note that we could have used a mutable here, but that would effectively
    // "duplicate state"
    let private stateChangedEvent = Event<_>()

    let stateManager = 
        new MailboxProcessor<_>(fun inbox ->
            let updateState state = 
                // Trigger our new state has changed
                stateChangedEvent.Trigger state
                state

            let rec loop oldState = async {
                let! msg = inbox.Receive()
                match msg with 
                | Get(channel) -> 
                    channel.Reply oldState
                    return! loop(oldState)
                | Update(msg) ->
                    let state = 
                        match msg with
                        | Accept(r)-> { r with Status = Accepted } :: oldState |> List.except [| r |]            
                        | Reject(r) -> { r with Status = Rejected } :: oldState |> List.except [| r |]                    
                        |> updateState
                    return! loop(state)
                | PurgeHandled ->
                    let isUnhandled request = match request.Status with | Unknown -> true | _ -> false
                    let state = 
                        oldState 
                        |> List.filter isUnhandled                    
                        |> updateState
                    return! loop(state)
                | AddNew(name, hours) ->
                    let r = { Person = name ; ExpectedHours = hours ; Status = Unknown }
                    let state = r :: oldState |> updateState
                    return! loop(updateState state)
            }
                
            loop []
        )
    stateManager.Start()

    let stateChanged = stateChangedEvent.Publish

// This is to simulate "external" influence on our model data
module internal AutoUpdater =
    let private rnd = Random()
    let possibleChars = "abcdefghijklmnopqrstuvwxyz".ToCharArray()

    let generateRandomString () =
        let charCount = 20
        Array.init charCount (fun _ -> possibleChars.[rnd.Next(possibleChars.Length)])
        |> String        

    let startUpdating () =
        async {
            // 0.5-1.5 seconds sleep between additions
            do! Async.Sleep <| 500 + rnd.Next(1000)
            let duration = rnd.NextDouble() * 500.0
            let name = generateRandomString ()

            State.AddNew(name, duration) |> State.stateManager.Post 

        } |> Async.Start

    let startPurging () =
        async {
            // 5-10 seconds sleep between purges of accepted/rejected items
            do! Async.Sleep <| 5000 + rnd.Next(5000)
        } |> Async.Start

module Model =
    // Initialization function
    let init () =
        AutoUpdater.startUpdating ()
        AutoUpdater.startPurging ()

    // Function to update the current state
    let update = State.Update >> State.stateManager.Post 

    // Functions to get the current state
    let get () = State.stateManager.PostAndReply State.Get
    let getAsync () = State.stateManager.PostAndAsyncReply State.Get

    // Gets the state as a Signal
    let asSignal =       
        let current = get ()  
        Signal.fromObservable current State.stateChanged 
