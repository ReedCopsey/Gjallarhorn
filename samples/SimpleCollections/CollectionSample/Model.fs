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
        | Purge                                 // Purge all Accepted or Rejected items from the model
        | AddNew of string * float              // Add a new request
    
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
        let replace pf sf xs =
            let find = ref false
            let rec aux = function
                | [] -> []
                | x::xs -> if pf x then find := true;sf :: xs else x :: (aux xs)
            aux xs

        MailboxProcessor.Start(fun inbox ->
            let rec loop current = async {
                let! msg = inbox.Receive()

                match msg with 
                | Get replyChannel -> 
                    replyChannel.Reply current
                    return! loop current
                | Update(msg) ->
                    let state = 
                        match msg with
                        | Accept(r)-> replace ((=) r) ({ r with Status = Accepted }) current
                        | Reject(r) -> replace ((=) r) { r with Status = Rejected } current 
                        |> notifyStateUpdated
                    return! loop state
                | Purge ->
                    let isUnhandled request = match request.Status with | Unknown -> true | _ -> false
                    let state = 
                        current 
                        |> List.filter isUnhandled                    
                        |> notifyStateUpdated
                    return! loop state
                | AddNew(name, hours) ->
                    let r = { Person = name ; ExpectedHours = hours ; Status = Unknown }
                    let state = r :: current |> notifyStateUpdated
                    return! loop state 
            }
                                    
            loop [] )

    // Publish our event of changing states
    let stateChanged = stateChangedEvent.Publish

// This is to simulate "external" influence on our model data
module internal AutoUpdater =
    let private rnd = Random()
    let possibleChars = "abcdefghijklmnopqrstuvwxyz".ToCharArray()

    let generateRandomString () =
        let charCount = 20
        Array.init charCount (fun _ -> possibleChars.[rnd.Next(possibleChars.Length)])
        |> String        

    // Add a random new elmenet to the list on a regular basis
    let startUpdating () =
        async {
            // 1.5-2.5 seconds sleep between additions
            while true do
                do! Async.Sleep <| 1500 + rnd.Next(1000)
                let duration = rnd.NextDouble() * 500.0
                let name = generateRandomString ()

                State.AddNew(name, duration) |> State.stateManager.Post 
        } |> Async.Start

    // Purge processed elements from the list as time goes by at random intervals
    let startPurging () =
        async {
            // 5-10 seconds sleep between purges of accepted/rejected items
            while true do
                do! Async.Sleep <| 5000 + rnd.Next(5000)
                State.Purge |> State.stateManager.Post 
        } |> Async.Start

module Model =
    // Initialization function - Kick off our routines to add and remove data
    let init () =
        AutoUpdater.startUpdating ()
        AutoUpdater.startPurging ()

    // Function to update the current state
    let update = State.Update >> State.stateManager.Post 

    // Functions to get the current state
    let get () = State.stateManager.PostAndReply State.Get
    let getAsync () = State.stateManager.PostAndAsyncReply State.Get

    // Gets the state as a Signal
    let asSignal () =       
        let current = get ()  
        Signal.fromObservable current State.stateChanged 
