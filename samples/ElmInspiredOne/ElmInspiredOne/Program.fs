namespace ElmInspiredOne

open System
open Gjallarhorn
open Gjallarhorn.Bindable

// Note that this program is defined in a PCL, and is completely platform neutral.
// It will work unchanged on WPF, Xamarin Forms, etc

module Program =
    // ----------------------------------     Model     ---------------------------------- 
    // Model is a simple integer for counter
    type Model = int
    let initModel i : Model = i

    // ----------------------------------    Update     ---------------------------------- 
    // We define a union type for each possible message
    type Msg = 
        | Increment 
        | Decrement

    // Create a function that updates the model given a message
    let update msg (model : Model) =
        match msg with
        | Increment -> model + 1
        | Decrement -> model - 1

    // ----------------------------------    Binding    ---------------------------------- 
    // Create a function that binds a model to a source, and outputs messages
    // This essentially acts as our "view" in Elm terminology, though it doesn't actually display 
    // the view as much as map from our type to bindings
    let bindToSource source (model : ISignal<Model>) =    
        // Create a property to display our current value    
        Binding.toView source "Current" model

        // Create commands for our buttons
        [
            Binding.createMessage "Increment" Increment source
            Binding.createMessage "Decrement" Decrement source
        ]

    // ----------------------------------   Framework  -----------------------------------     
    let applicationCore = Framework.info (initModel 5) update bindToSource 
