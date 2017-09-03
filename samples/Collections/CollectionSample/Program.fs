namespace CollectionSample

open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Bindable.Framework
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

    // We start with a "ViewModel" for cleaner bindings and XAML support
    type RequestViewModel =
        {
            Id : Guid
            Hours : float
            Status : Status
            Accept : Cmd<Operations.RequestUpdate>
            Reject : Cmd<Operations.RequestUpdate>
        }
    let reqd = { Id = Guid.NewGuid() ; Hours = 45.32 ; Status = Status.Accepted ; Accept = Cmd.ofMsg Operations.AcceptRequest ; Reject = Cmd.ofMsg Operations.RejectRequest }
    
    // Create a component for a single request
    let requestComponent =
        [
            <@ reqd.Id @>       |> Bind.oneWay (fun (r : Request) -> r.Id)
            <@ reqd.Hours @>    |> Bind.oneWay (fun r -> r.ExpectedHours)
            <@ reqd.Status @>   |> Bind.oneWay (fun r -> r.Status)
            <@ reqd.Accept @>   |> Bind.cmd 
            <@ reqd.Reject @>   |> Bind.cmd 
        ] |> Component.FromBindings

    type RequestsViewModel =
        {
            Requests : Request seq 
        }
    let reqsd = { Requests = [] }
    
    // Create the component for the Requests as a whole.
    // Note that this uses BindingCollection to map the collection to individual request -> messages,
    // using the component defined previously, then maps this to the model-wide update message.
    let requestsComponent = //source (model : ISignal<Requests>) =
        let sorted (requests : Requests) = requests |> Seq.sortBy (fun r -> r.Created)
        [
            <@ reqsd.Requests @> |> Bind.collection sorted requestComponent Operations.requestUpdateToUpdate
        ] 
        |> Component.FromBindings

    type extVM = { AddingRequests : bool ; Processing : bool }
    let  extD = { AddingRequests = false ; Processing = false }
    let externalComponent =                
        [
            <@ extD.AddingRequests @> |> Bind.twoWay (fun (m : Model) -> Option.isSome m.AddingRequests.Operating) (Executing >> AddRequests)
            <@ extD.Processing     @> |> Bind.twoWay (fun m -> Option.isSome m.Processing.Operating)               (Executing >> ProcessRequests)
        ] 
        |> Component.FromBindings

    type AppViewModel =
        {
            Requests : Requests
            Updates : Model
        }
    let appd = { Requests = [] ; Updates = { Requests = [] ; AddingRequests = {Operating = None} ; Processing = { Operating = None } } }

    /// Compose our components above into one application level component
    let appComponent =
        [
            <@ appd.Requests @> |> Bind.comp (fun (m : Model) -> m.Requests) requestsComponent (fst >> Msg.Update)
            <@ appd.Updates @>  |> Bind.comp id externalComponent fst
        ] 
        |> Component.FromBindings

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
        Framework.application state.ToSignal state.Initialize state.Update appComponent
