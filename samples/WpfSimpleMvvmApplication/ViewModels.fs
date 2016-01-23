namespace ViewModels

open Gjallarhorn
open Gjallarhorn.Bindable

open Gjallarhorn.Validation

type NameModel = { First : string ; Last : string }

module VM =
    let createMain (name : IMutatable<NameModel>) =
        // Create a binding target equivelent to https://github.com/fsprojects/FsXaml/blob/master/demos/WpfSimpleMvvmApplication/MainViewModel.fs
        let bt = Bind.create()

        // Create the "properties" we want to bind to - this could be mutables, views (for read-only), or commands
        let first = 
            name
            |> View.map (fun n -> n.First) 
            |> bt.BindEditor "FirstName" (notNullOrWhitespace >> noSpaces >> notEqual "Reed") 
        let last = 
            name
            |> View.map (fun n -> n.Last) 
            |> bt.BindEditor "LastName" (notNullOrWhitespace >> fixErrors >> hasLengthAtLeast 3 >> noSpaces)

        // Read only properties can optionally be validated as well, allowing for "entity level" validation
        (fun f l -> f + " " + l) <!> first <*> last
        |> View.validate (notEqual "Reed Copsey" >> fixErrorsWithMessage "That is a poor choice of names")
        |> bt.BindView "FullName"

        // This is our "result" from the UI (includes invalid results)
        // As the user types, this constantly updates
        let name' = (fun f l -> {First = f; Last = l}) <!> first <*> last

        // Create a command that will only execute if
        // 1) We're valid and 2) our name has changed from the input
        let canExecute = 
            View.notEqual name name'
            |> View.both bt.Valid
        let okCommand = Command.create canExecute
        okCommand |> bt.BindCommand "OkCommand"                

        // Uncomment the following to automatically push back all changes to 
        // source "name" mutable without requiring the button click
//        name'
//        |> View.filter (fun _ -> bt.IsValid)
//        |> View.copyTo name
//        |> bt.TrackDisposable
        
        // Subscribe to our command to push values back to our source mutable
        okCommand 
        |> Command.subscribe (fun _ -> name.Value <- name'.Value)
        |> bt.TrackDisposable

        // Return the binding target for use as a View Model
        bt

// This provides a view-first approach to creating and supplying a ViewModel
// Currently unused in this sample.
type ViewModelFactory() =
    inherit BindingTargetFactory()
    
    override __.Generate() = 
        let name = Mutable.create {First = "foo" ; Last = "bar"}
        VM.createMain name