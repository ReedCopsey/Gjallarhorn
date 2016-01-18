namespace ViewModels

open Gjallarhorn
open Gjallarhorn.Bindable

open Gjallarhorn.Validation

module VM =
    let createMain() =
        // Create a binding target equivelent to https://github.com/fsprojects/FsXaml/blob/master/demos/WpfSimpleMvvmApplication/MainViewModel.fs
        let bt = Bind.create()

        let first = Mutable.createValidated (notNullOrWhitespace >> noSpaces >> notEqual "Reed") "Reed"
        let last = Mutable.createValidated (notNullOrWhitespace >> fixErrors >> hasLengthAtLeast 3 >> noSpaces) "Copsey"

        let full = 
            (fun f l -> f + " " + l) <!> first <*> last
            |> View.validate (notEqual "Reed Copsey" >> fixErrorsWithMessage "That is a poor choice of names")

        let exec _ = 
            System.GC.Collect();
            System.Windows.MessageBox.Show(sprintf "Hello, %s!" full.Value) |> ignore
    
        let comm = new ViewCommand(bt.Valid)

        comm
        |> View.subscribe exec
        |> ignore
    //    let comm = 
    //        Command.create exec
    //        |> Command.filter bt.Valid
    //        |> Command.track bt

        bt
        |> Bind.edit "FirstName" first 
        |> Bind.edit "LastName" last 
        |> Bind.watch "FullName" full
        |> Bind.command "OkCommand" comm

type ViewModelFactory() =
    inherit BindingTargetFactory()

    override this.Generate() = VM.createMain()