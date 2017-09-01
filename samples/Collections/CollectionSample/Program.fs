namespace CollectionSample

open Gjallarhorn
open Gjallarhorn.Bindable
open CollectionSample.RequestModel
open CollectionSample.External
open System

// Note that this program is defined in a PCL, and is completely platform neutral.
// It will work unchanged on WPF, Xamarin Forms, etc

module Program =
    
    let [<Literal>] minAddTime = 1500
    let [<Literal>] addRandomness = 1500
    let [<Literal>] processThreshold = 3.0

    // Generate a message to add a random new elmenet to the list on a regular basis
    // In a "real" application, this would likely be doing something like
    // asynchronously calling out to a service and adding in new items
    // Returns a token source used to stop generating
    let startUpdating (source : ObservableSource<_>) () =
        let rnd = Random()
        let cts = new System.Threading.CancellationTokenSource()
        let wf = async {
            while true do
                // minAddTime to (minAddTime+addRandomness) seconds sleep between additions
                do! Async.Sleep <| minAddTime + rnd.Next(addRandomness)                                
                
                Operations.AddNew(Guid.NewGuid(), rnd.NextDouble() * 500.0)
                |> source.Trigger
        }

        Async.Start(wf, cancellationToken = cts.Token)
        cts

    // Purge processed elements from the list as time goes by at random intervals
    let startProcessing (source : ObservableSource<_>) () =
        let cts = new System.Threading.CancellationTokenSource()
        let wf = async {
            // On half second intervals, purge anything processed more than processThreshold seconds ago
            while not cts.IsCancellationRequested do
                do! Async.Sleep 250

                TimeSpan.FromSeconds(processThreshold)
                |> Operations.Process
                |> source.Trigger
        }

        Async.Start(wf, cancellationToken = cts.Token)
        cts

    // ----------------------------------    Binding    ---------------------------------- 
    // Create a function that binds a model to a source, and outputs messages
    // This essentially acts as our "view" in Elm terminology, though it doesn't actually display 
    // the view as much as map from our type to bindings
    
    // Create a component for a single request
    let requestComponent source (model : ISignal<Request>) =         
        // Bind the properties we want to display
        model |> Signal.map (fun v -> v.Id)            |> Binding.toView source "Id"
        model |> Signal.map (fun v -> v.ExpectedHours) |> Binding.toView source "Hours"
        model |> Signal.map (fun v -> v.Status)        |> Binding.toView source "Status"
            
        [
            source |> Binding.createMessage "Accept" Operations.AcceptRequest
            source |> Binding.createMessage "Reject" Operations.RejectRequest
        ]

    // Create the component for the Requests as a whole.
    // Note that this uses BindingCollection to map the collection to individual request -> messages,
    // using the component defined previously, then maps this to the model-wide update message.
    let requestsComponent source (model : ISignal<Requests>) =    
        let sorted = 
            model
            |> Signal.map (Seq.sortBy (fun req -> req.Created))

        // Create a property to display our current value    
        [
            BindingCollection.toView source "Requests" sorted (Component.FromObservables requestComponent)
            |> Observable.map Operations.requestUpdateToUpdate 
        ]

    let externalComponent source (model : ISignal<Model>) =
        let updating = 
            model
            |> Signal.map (fun m -> Option.isSome m.AddingRequests.Operating)
            |> Binding.toFromView source "AddingRequests"
        let processing = 
            model
            |> Signal.map (fun m -> Option.isSome m.Processing.Operating)
            |> Binding.toFromView source "Processing"

        [ 
            updating |> Observable.map (Executing >> AddRequests)
            processing |> Observable.map (Executing >> ProcessRequests)
        ]

    /// Compose our components above into one application level component
    let appComponent source (model : ISignal<Model>) =        
        let requestUpdates =            
            model 
            |> Signal.map (fun m -> m.Requests)
            |> Binding.componentToView source "Requests" (Component.FromObservables requestsComponent)
        let externalUpdates =
            model             
            |> Binding.componentToView source "Updates" (Component.FromObservables externalComponent)

        [
            requestUpdates |> Observable.map Msg.Update
            externalUpdates 
        ]

    // ----------------------------------   Framework  -----------------------------------     
    
    let applicationCore fnAccepted fnRejected = 
        // Create a source for Update messages
        let messageSource = ObservableSource<Operations.Update>()

        // Create functions via partial application to start updating and processing
        let upd = startUpdating messageSource
        let proc = startProcessing messageSource

        // Create our state
        let state = CollectionSample.StateManagement(fnAccepted, fnRejected, upd, proc)
                
        // Map messages from our source into the state update routine
        // This allows our external functions to generate messages
        // separate from the "application" as needed.
        messageSource
        |> Observable.add (fun msg -> Update msg |> state.Update)

        // Start and run our application
        Framework.application state.ToSignal state.Initialize state.Update (Component.FromObservables appComponent)
