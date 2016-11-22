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
    let bindToSource source (model : ISignal<Requests>) =    
        let sorted = 
            model
            |> Signal.map (Seq.sortBy (fun req -> req.Created))
        // Create a property to display our current value    
        Binding.toView source "Requests" sorted
        
        // Create commands for our buttons
        [
            Binding.createMessageParam "Accept" Operations.Accept source
            Binding.createMessageParam "Reject" Operations.Reject source
        ]

    // ----------------------------------   Framework  -----------------------------------     
    let applicationCore fnAccepted fnRejected = Framework.application StateManagement.asSignal (fun _ -> StateManagement.initExternalUpdates fnAccepted fnRejected) State.update bindToSource 
