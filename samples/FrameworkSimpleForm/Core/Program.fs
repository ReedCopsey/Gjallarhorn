namespace SimpleForm

open System
open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Validation
open Gjallarhorn.Validation.Validators

// Note that this program is defined in a PCL, and is completely platform neutral.
// It will work unchanged on WPF, Xamarin Forms, etc

module Program =
    // ----------------------------------     Model     ---------------------------------- 
    // Model contains our first and last name
    type Model = 
        { 
            FirstName : string 
            LastName : string 
        }
    with 
        static member Default = { FirstName = "" ; LastName = "" }

    // ----------------------------------    Update     ---------------------------------- 
    // We define a union type for each possible message
    type Msg = 
        | FirstName of string
        | LastName of string

    // Create a function that updates the model given a message
    let update msg (model : Model) =
        match msg with
        | FirstName f -> { model with FirstName = f }
        | LastName l -> { model with LastName = l }

    // ----------------------------------    Binding    ---------------------------------- 
    // Create a function that binds a model to a source, and outputs messages
    let bindToSource source (model : ISignal<Model>) =    
        // Bind our properties, with validation, to the view        
        let first = 
            model 
            |> Binding.memberToFromView source <@ model.Value.FirstName @> notNullOrWhitespace
            // These are IObservable<string option> - so map valid changes to our message type
            |> Observable.toMessage FirstName

        let last = 
            model 
            |> Binding.memberToFromView source <@ model.Value.LastName @> (notNullOrWhitespace >> notEqual "Copsey") // Composable validation
            |> Observable.toMessage LastName

        // Allow the display our (validated) full name
        model
        |> Signal.map (fun m -> m.FirstName + " " + m.LastName)
        |> Binding.toViewValidated source "FullName" (notNullOrWhitespace >> fixErrorsWithMessage "Please enter a valid name")

        // Return our output streams
        [ first ; last ]

    // ----------------------------------   Framework  -----------------------------------     
    let applicationCore = Framework.basicApplication Model.Default update bindToSource 
