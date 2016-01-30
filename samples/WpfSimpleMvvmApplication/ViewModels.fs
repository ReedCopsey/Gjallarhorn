namespace ViewModels

open System
open Gjallarhorn
open Gjallarhorn.Bindable

open Gjallarhorn.Validation

type NameModel = { First : string ; Last : string }

module VM =
    let createMainViewModel (nameIn : IObservable<NameModel>) =
        // Create a binding target equivelent to https://github.com/fsprojects/FsXaml/blob/master/demos/WpfSimpleMvvmApplication/MainViewModel.fs
        let bt = Bind.create()

        // Create the "properties" we want to bind to - this could be mutables, signals (for read-only), or commands
        let name, handle = Signal.subscribeFromObservable { First = "" ; Last = "" } nameIn
        bt.TrackDisposable handle
        let first = 
            name
            |> Signal.map (fun n -> n.First) 
            |> Signal.validate (notNullOrWhitespace >> noSpaces >> notEqual "Reed") 
            |> bt.Bind "FirstName"
        let last = 
            name
            |> Signal.map (fun n -> n.Last) 
            |> Signal.validate (notNullOrWhitespace >> fixErrors >> hasLengthAtLeast 3 >> noSpaces)
            |> bt.Bind "LastName"

        // Read only properties can optionally be validated as well, allowing for "entity level" validation
        Signal.map2 (fun f l -> f + " " + l) first last
        |> Signal.validate (notEqual "Reed Copsey" >> fixErrorsWithMessage "That is a poor choice of names")
        |> bt.Bind "FullName"
        |> ignore

        // This is our "result" from the UI (includes invalid results)
        // As the user types, this constantly updates
        let name' = Signal.map2 (fun f l -> {First = f; Last = l}) first last

        // Create a command that will only execute if
        // 1) We're valid and 2) our name has changed from the input
        let canExecute = 
            Signal.notEqual name name'
            |> Signal.both bt.Valid
        let okCommand = Command.create canExecute
        okCommand |> bt.Constant "OkCommand"                

        let nameOut = Mutable.create name.Value

        // Subscribe to our command to push values back to our source mutable
        okCommand 
        |> Command.subscribe (fun _ -> nameOut.Value <- name'.Value)
        |> bt.TrackDisposable

        // Uncomment the following to automatically push back all changes to 
        // source "name" mutable without requiring the button click
//        name'
//        |> Signal.filter (fun _ -> bt.IsValid)
//        |> Signal.copyTo nameOut
//        |> bt.TrackDisposable
        

        // Return the binding target for use as a View Model
        bt, nameOut :> ISignal<NameModel>

