namespace ViewModels

open System
open Gjallarhorn
open Gjallarhorn.Bindable

open Gjallarhorn.Validation

type NameModel = { First : string ; Last : string }
with
    override this.ToString() =
        sprintf "Name: [%s] [%s]" this.First this.Last

module VM =
    let createMainViewModel (nameIn : IObservable<NameModel>) =
        // Create a binding target equivelent to https://github.com/fsprojects/FsXaml/blob/master/demos/WpfSimpleMvvmApplication/MainViewModel.fs
        let subject = Bind.createSubject ()

        // Map our incoming IObservable to a signal
        let name = subject.ObservableToSignal { First = "" ; Last = "" } nameIn

        // Create the "properties" we want to bind to - this could be mutables, signals (for read-only), or commands
        let first = 
            name
            |> Signal.map (fun n -> n.First)             
            |> subject.Edit "FirstName" (notNullOrWhitespace >> noSpaces >> notEqual "Reed") 
        let last = 
            name
            |> Signal.map (fun n -> n.Last)             
            |> subject.Edit "LastName" (notNullOrWhitespace >> fixErrors >> hasLengthAtLeast 3 >> noSpaces)

        // Read only properties can optionally be validated as well, allowing for "entity level" validation
        Signal.map2 (fun f l -> f + " " + l) first last
        |> Signal.validate (notEqual "Ree Copsey" >> fixErrorsWithMessage "That is a poor choice of names")
        |> subject.Watch "FullName"        

        // This is our "result" from the UI (includes invalid results)
        // As the user types, this constantly updates
        let name' = Signal.map2 (fun f l -> {First = f; Last = l}) first last

        // Create a command that will only execute if
        // 1) We're valid and 2) our name has changed from the input
        let canExecute = 
            Signal.notEqual name name'
            |> Signal.both subject.Valid
        let okCommand = subject.CommandChecked "OkCommand" canExecute

        // Change the following to automatically push back all changes to 
        // source "name" mutable without requiring the button click
        let pushAutomatically = false
        match pushAutomatically with
        | true ->
            // To push automatically, we output the signal whenever it's valid as an observable
            name'
            |> Signal.filter (fun _ -> subject.IsValid)
            |> subject.OutputObservable
        | false ->
            // In this case, we can use our command to map the right value out when it's clicked
            okCommand
            |> Signal.map (fun _ -> name'.Value)
            |> subject.OutputObservable

        // Return the binding subject for use as a View Model
        subject

