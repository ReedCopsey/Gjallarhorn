namespace CollectionSample

open Gjallarhorn
open Gjallarhorn.Bindable
open CollectionSample.Model

// Note that this program is defined in a PCL, and is completely platform neutral.
// It will work unchanged on WPF, Xamarin Forms, etc

module Program =
    
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
            BindingCollection.toView source "Requests" sorted requestComponent 
            |> Observable.map Operations.requestUpdateToUpdate 
        ]

    // ----------------------------------   Framework  -----------------------------------     
    
    let applicationCore fnAccepted fnRejected = 
        let state = CollectionSample.StateManagement(fnAccepted, fnRejected)
        Framework.application state.ToSignal state.Initialize state.Update requestsComponent 
