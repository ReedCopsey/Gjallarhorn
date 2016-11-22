namespace CollectionSample.External

open System
open CollectionSample
open CollectionSample.Model

// This is to simulate "external" influence on our model data
module internal ExternalUpdater =    
    // Add a random new elmenet to the list on a regular basis
    // In a "real" application, this would likely be doing something like
    // asynchronously calling out to a service and adding in new items
    let startUpdatingLoop () =
        let rnd = Random()
        async {            
            while true do
                // 2.5-5.0 seconds sleep between additions
                do! Async.Sleep <| 2500 + rnd.Next(2500)                                
                
                Operations.AddNew(Guid.NewGuid(), rnd.NextDouble() * 500.0)
                |> State.Update 
                |> State.stateManager.Post 
        } |> Async.Start

    // Purge processed elements from the list as time goes by at random intervals
    let startProcessingLoop (handleAccepted : Request -> unit) (handleRejected : Request -> unit) =
        async {
            // On half second intervals, purge anything processed more than 5 seconds ago
            while true do
                do! Async.Sleep 500
                let handle r = 
                    match r.Status with 
                    | Accepted -> handleAccepted r
                    | Rejected -> handleRejected r
                    | _ -> failwith "Unknown request processed by state manager"

                State.stateManager.PostAndReply (fun c -> State.Process(c, TimeSpan.FromSeconds(5.0))) 
                |> Seq.iter handle
        } |> Async.Start
