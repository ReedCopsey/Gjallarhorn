namespace CollectionSample.External

open System
open CollectionSample.RequestModel
open System.Threading

type UpdateExternal =
    | SetUpdating of bool
    | SetProcessing of bool

// This is handled as nothing more than another set of messages and a new model
type ExternalModel = 
    { 
        Updating : CancellationTokenSource option 
        Processing : CancellationTokenSource option 
        Updater : ExternalUpdater 
    }
and ExternalUpdater () =

    // To trigger our message
    let evt = Event<_>()

    // Generate a message to add a random new elmenet to the list on a regular basis
    // In a "real" application, this would likely be doing something like
    // asynchronously calling out to a service and adding in new items
    // Returns a token source used to stop generating
    let startUpdating () =
        let rnd = Random()
        let cts = new System.Threading.CancellationTokenSource()
        let wf = async {
            while true do
                // 2.5-5.0 seconds sleep between additions
                do! Async.Sleep <| 2500 + rnd.Next(2500)                                
                
                Operations.AddNew(Guid.NewGuid(), rnd.NextDouble() * 500.0)
                |> evt.Trigger
        }

        Async.Start(wf, cancellationToken = cts.Token)
        cts

    // Purge processed elements from the list as time goes by at random intervals
    let startProcessing () =
        let cts = new System.Threading.CancellationTokenSource()
        let wf = async {
            // On half second intervals, purge anything processed more than 5 seconds ago
            while not cts.IsCancellationRequested do
                do! Async.Sleep 500

                TimeSpan.FromSeconds(5.0)
                |> Operations.Process
                |> evt.Trigger
        }

        Async.Start(wf, cancellationToken = cts.Token)
        cts

    member __.Update msg (current : ExternalModel) = 
        match msg with
        | SetUpdating upd -> 
            match current.Updating, upd with
            | None, true -> 
                // Start the processing loop
                { current with Updating = Some (startUpdating ()) }
            | Some cts, false -> 
                // Stop the processing loop
                cts.Cancel()
                { current with Updating = None }
            | None, false 
            | Some _, true -> 
                current
        | SetProcessing proc -> 
            match current.Processing, proc with
            | None, true -> 
                // Start the processing loop
                { current with Processing = Some (startProcessing ()) }
            | Some cts, false -> 
                // Stop the processing loop
                cts.Cancel()
                { current with Processing = None }
            | None, false 
            | Some _, true -> 
                current

    // Provide a mechanism to publish update requests
    member __.Updates with get() = evt.Publish :> IObservable<_>
    