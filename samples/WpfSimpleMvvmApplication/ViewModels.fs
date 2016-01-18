namespace ViewModels

open Gjallarhorn
open Gjallarhorn.Bindable

open Gjallarhorn.Validation

module VM =
    let createMain() =
        // Create a binding target equivelent to https://github.com/fsprojects/FsXaml/blob/master/demos/WpfSimpleMvvmApplication/MainViewModel.fs
        let bt = Bind.create()

        // Create the "properties" we want to bind to - this could be mutables, views (for read-only), or commands
        let first = Mutable.createValidated (notNullOrWhitespace >> noSpaces >> notEqual "Reed") "Reed"
        let last = Mutable.createValidated (notNullOrWhitespace >> fixErrors >> hasLengthAtLeast 3 >> noSpaces) "Copsey"
        let full = 
            (fun f l -> f + " " + l) <!> first <*> last
            |> View.validate (notEqual "Reed Copsey" >> fixErrorsWithMessage "That is a poor choice of names")
        let okCommand = Command.create bt.Valid        
        
        // Subscribe to our command to perform app-specific logic
        let handler = 
            okCommand 
            |> Command.subscribe (fun time -> System.Windows.MessageBox.Show(sprintf "Hello, %s!  It's %A" full.Value time) |> ignore)
                
        Bind.addBindings bt {
            edit "FirstName" first
            edit "LastName" last
            watch "FullName" full
            command "OkCommand" okCommand

            // These will get disposed when our binding target is disposed
            dispose handler
            dispose full
        }        

type ViewModelFactory() =
    inherit BindingTargetFactory()

    override __.Generate() = VM.createMain()