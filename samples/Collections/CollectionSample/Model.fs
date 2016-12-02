namespace CollectionSample

// Note that this program is defined in a PCL, and is completely platform neutral.
// It will work unchanged on WPF, Xamarin Forms, etc

// This is definitely on the "complicated" side, but shows how to use multiple 
// mailbox processors, defined outside of the main UI, to manage a model
// defined itself as a collection

namespace CollectionSample.Model

open System
open Gjallarhorn

type Status =
    | Unknown
    | Accepted
    | Rejected

type Request = 
        { 
            Id : Guid // What is our unique identifier
            Created : DateTime 
            ExpectedHours : float 
            Status : Status 
            StatusUpdated : DateTime option 
        }    

type Requests = Request list

// Operations on our model
module Operations =
    type RequestUpdate =
        | AcceptRequest
        | RejectRequest
    
    // These are the updates that can be performed on our requests
    type Update = 
        | Accept of Request
        | Reject of Request
        | AddNew of Guid * float
        | Process of TimeSpan

    // Maps from an accept/rejection of a single request to an
    // update message for the model as a whole
    let requestUpdateToUpdate (ru : RequestUpdate, req : Request) =
        match ru with
        | AcceptRequest -> Accept req
        | RejectRequest -> Reject req

    // Update the model based on an UpdateRequest
    let update processAccepted processRejected msg current =
        let excluded r = current |> List.except [| r |]
        match msg with
        | Accept(r)-> 
            { r with Status = Accepted ; StatusUpdated = Some(DateTime.UtcNow)} :: excluded r
        | Reject(r) -> 
            { r with Status = Rejected ; StatusUpdated = Some(DateTime.UtcNow)} :: excluded r
        | AddNew(guid, hours) -> 
            let r : Request = { Id = guid ; Created = DateTime.UtcNow ; ExpectedHours = hours ; Status = Unknown ; StatusUpdated = None }
            r :: current 
        | Process(minLife) ->
            let shouldKeep life (request : Request) = 
                match request.Status, request.StatusUpdated with 
                | Unknown, _ -> true
                | _, Some upd when upd < (DateTime.UtcNow - life) -> 
                    false // Only purge if it's old enough, and not unknown
                | _ -> true
            let state, removed =
                current
                |> List.partition (shouldKeep minLife)

            let processRemoved req =
                match req.Status with
                | Accepted -> processAccepted req
                | Rejected -> processRejected req
                | _ -> failwith "Unknown status hit processing stage"

            removed
            |> List.iter processRemoved

            state
