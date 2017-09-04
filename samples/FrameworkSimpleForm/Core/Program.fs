namespace SimpleForm

open System
open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Bindable.Framework
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

    // Our "ViewModel". This is optional, but allows you to avoid using "magic strings", as well as enables design time XAML in C# projects
    [<CLIMutable>] // CLIMutable is required by XAML tooling if we have 2-way bindings
    type ViewModel = 
        {
            FirstName   : string
            LastName    : string
            FullName    : string
        }    

    // This is our design/compile time ViewModel used for XAML and binding for naming
    let d = { FirstName = "Reed" ; LastName="Copsey" ; FullName = "Reed Copsey" }

    // ----------------------------------    Binding    ---------------------------------- 
    // Create a function that binds a model to a source, and outputs messages
    let bindToSource =    
        // Composable validation - Can be written inline as well
        let validLast = notNullOrWhitespace >> notEqual "Copsey" 
        let validFull = notNullOrWhitespace >> fixErrorsWithMessage "Please enter a valid name"

        Component.fromBindings<Model,Msg> [
            <@ d.FirstName @>  |> Bind.twoWayValidated (fun m -> m.FirstName) notNullOrWhitespace FirstName
            <@ d.LastName @>   |> Bind.twoWayValidated (fun m -> m.LastName) validLast LastName
            <@ d.FullName @>   |> Bind.oneWayValidated (fun m -> m.FirstName + " " + m.LastName) validFull
        ]   

    // ----------------------------------   Framework  -----------------------------------     
    let applicationCore = Framework.basicApplication Model.Default update bindToSource
