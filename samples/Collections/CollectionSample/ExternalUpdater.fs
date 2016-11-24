namespace CollectionSample.External

open System
open Gjallarhorn
open CollectionSample.Model

// This is to simulate "external" influence on our model data
module internal ExternalUpdater =    
    // Add a random new elmenet to the list on a regular basis
    // In a "real" application, this would likely be doing something like
    // asynchronously calling out to a service and adding in new items
    let startUpdatingLoop (state : State<Requests,Operations.Update>) =
        let rnd = Random()
        async {            
            while true do
                // 2.5-5.0 seconds sleep between additions
                do! Async.Sleep <| 2500 + rnd.Next(2500)                                
                
                Operations.AddNew(Guid.NewGuid(), rnd.NextDouble() * 500.0)
                |> state.Update
                |> ignore
        } |> Async.Start

    // Purge processed elements from the list as time goes by at random intervals
    let startProcessingLoop (state : State<Requests,Operations.Update>) =
        async {
            // On half second intervals, purge anything processed more than 5 seconds ago
            while true do
                do! Async.Sleep 500

                do! 
                    TimeSpan.FromSeconds(5.0)
                    |> Operations.Process
                    |> state.UpdateAsync
                    |> Async.Ignore
        } |> Async.Start
