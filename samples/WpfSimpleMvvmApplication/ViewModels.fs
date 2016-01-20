namespace ViewModels

open Gjallarhorn
open Gjallarhorn.Bindable

open Gjallarhorn.Validation

type Name = { First : string ; Last : string }

module VM =
    let createMain() =
        // Create a binding target equivelent to https://github.com/fsprojects/FsXaml/blob/master/demos/WpfSimpleMvvmApplication/MainViewModel.fs
        let bt = Bind.create()

        let name = Mutable.create { First = "Foo" ; Last = "Bar" }

        // Create the "properties" we want to bind to - this could be mutables, views (for read-only), or commands
        let first = 
            name
            |> Mutable.step (fun n -> n.First) (fun c f -> {c with First = f})
            |> Mutable.validate (notNullOrWhitespace >> noSpaces >> notEqual "Reed") 
        let last = 
            name
            |> Mutable.step (fun n -> n.Last) (fun c l -> {c with Last = l})
            |> Mutable.validate (notNullOrWhitespace >> fixErrors >> hasLengthAtLeast 3 >> noSpaces)
        let full = 
            (fun f l -> f + " " + l) <!> first <*> last
            |> View.validate (notEqual "Reed Copsey" >> fixErrorsWithMessage "That is a poor choice of names")
        let okCommand = Command.create bt.Valid        
        
        // Subscribe to our command to perform app-specific logic
        let handler = 
            okCommand 
            |> Command.subscribe (fun time -> System.Windows.MessageBox.Show(sprintf "Hello, %A!  It's %A" name.Value time) |> ignore)
                
        Bind.extend bt {
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