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
    let createMainViewModel (nameIn : IObservable<NameModel>) initialValue =
        // Create a binding target equivelent to https://github.com/fsprojects/FsXaml/blob/master/demos/WpfSimpleMvvmApplication/MainViewModel.fs
        let subject = Bind.createSubject ()
        
        // Map our incoming IObservable to a signal. If we didn't want to use an input IObservable, we could just use Signal.constant or Mutable.create
        let source = subject.ObservableToSignal initialValue nameIn

        // Create the "properties" we want to bind to - this could be mutables, signals (for read-only), or commands
        let first = 
            source
            |> subject.EditMember <@ initialValue.First @> (notNullOrWhitespace >> noSpaces >> notEqual "Reed") 
        let last = 
            source
            |> subject.EditMember <@ initialValue.Last @> (notNullOrWhitespace >> fixErrors >> hasLengthAtLeast 3 >> noSpaces)

        // Combine edits on properties into readonly properties to be validated as well, allowing for "entity level" validation or display
        Signal.map2 (fun f l -> f + " " + l) first last
        |> Signal.validate (notEqual "Ree Copsey" >> fixErrorsWithMessage "That is a poor choice of names")
        |> subject.Watch "Full"        

        // This is our "result" from the UI (includes invalid results)
        // As the user types, this constantly updates
        let name' = Signal.map2 (fun f l -> {First = f; Last = l}) first last

        // Create a "toggle" which we can use to toggle whether to push automatically to the backend
        let pushAutomatically = Mutable.create false        
        
        // Bind it directly and push changes back to the input mutable
        subject.BindDirect "PushAutomatically" pushAutomatically
        
        let pushManually = Signal.not pushAutomatically

        // Create a command that will only execute if
        // 1) We're valid, 2) we're not pusing automatically, and 3) our name has changed from the input
        let canExecute = 
            Signal.notEqual source name'
            |> Signal.both pushManually
            |> Signal.both subject.Valid
        let okCommand = subject.CommandChecked "OkCommand" canExecute

        let automaticUpdates =
            // To push automatically, we output the signal whenever it's valid as an observable
            // Note that we use FilterValid to guarantee that all validation is completed before
            // the final signal is sent through.  This isn't a problem with the command approach,
            // but guarantees our validation to always be up to date before it's queried in the filter
            name'
            |> subject.FilterValid
            |> Observable.filter (fun _ -> pushAutomatically.Value)
        // Command-triggered updates
        let commandUpdates =
            // In this case, we can use our command to map the right value out when it's clicked
            // Since the command already is only enabled when we're valid, we don't need a validity filter here
            okCommand
            |> Signal.filterBy pushManually 
            |> Observable.map (fun _ -> name'.Value)

        // Combine our automatic and manual updates into one signal, and push them to the backing observable
        Observable.merge automaticUpdates commandUpdates
        |> subject.OutputObservable

        // Return the binding subject for use as a View Model
        subject

