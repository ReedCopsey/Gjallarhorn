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
    // These are the updates that can be performed on our requests
    type Update = 
        | Accept of Request
        | Reject of Request
        | AddNew of Guid * float

    // Update the model based on an UpdateRequest
    let update msg current =
        let excluded r = current |> List.except [| r |]
        match msg with
        | Accept(r)-> 
            { r with Status = Accepted ; StatusUpdated = Some(DateTime.UtcNow)} :: excluded r
        | Reject(r) -> 
            { r with Status = Rejected ; StatusUpdated = Some(DateTime.UtcNow)} :: excluded r
        | AddNew(guid, hours) -> 
            let r : Request = { Id = guid ; Created = DateTime.UtcNow ; ExpectedHours = hours ; Status = Unknown ; StatusUpdated = None }
            r :: current 

    // Process all of the items in the model,
    // returning the new state, and the processed items
    // Returns newState*processedItems as 2 lists
    let partitionProcessed minLife current =
        let shouldKeep life (request : Request) = 
            match request.Status, request.StatusUpdated with 
            | Unknown, _ -> true
            | _, Some upd when upd < (DateTime.UtcNow - life) -> 
                false // Only purge if it's old enough, and not unknown
            | _ -> true
        current 
        |> List.partition (shouldKeep minLife)
    

