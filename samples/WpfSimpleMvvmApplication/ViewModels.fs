namespace ViewModels

open System

open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Validation
open Gjallarhorn.Validation.Validators
open Gjallarhorn.Observable

type NameModel = { First : string ; Last : string }
with
    override this.ToString() =
        sprintf "Name: [%s] [%s]" this.First this.Last

module VM =
    let createMainViewModel (nameIn : IObservable<NameModel>) initialValue =
        // Create an observable binding source
        let bindingSource = Binding.createObservableSource ()
        
        // This is optional, but lets us track changes easily
        let source = Mutable.create initialValue

        // Copy incoming changes from our input observable into our mutable
        nameIn
        |> Observable.Subscription.copyTo source
        |> bindingSource.AddDisposable

        // Create the "properties" we want to bind to - this could be mutables, signals (for read-only), or commands
        let first = 
            source 
            |> Binding.memberToFromView bindingSource <@ initialValue.First @> (notNullOrWhitespace >> noSpaces >> notEqual "Reed")
        let last  = 
            source 
            |> Binding.memberToFromView bindingSource <@ initialValue.Last  @> (notNullOrWhitespace >> fixErrors >> hasLengthAtLeast 3 >> noSpaces) 
                        
        // Combine edits on properties into readonly properties to be validated as well, allowing for "entity level" validation or display
        Signal.map2 (fun f l -> f + " " + l) first.RawInput last.RawInput
        |> Binding.toViewValidated bindingSource "Full" (notEqual "Ree Copsey" >> fixErrorsWithMessage "That is a poor choice of names")

        // This is our "result" from the UI
        // As the user types, this constantly updates whenever the output is valid and doesn't match the last known value
        let userChanges = 
            Signal.mapOption2 (fun f l -> {First = f; Last = l}) first last
            |> Observable.filterSome
            |> Observable.filter (fun v -> v <> source.Value)

        // We're storing the last "good" name from the user. Initializes using input value
        let lastGoodName = Signal.fromObservable source.Value userChanges            

        // Create a "toggle" which we can use to toggle whether to push automatically to the backend
        // and bind it directly and push changes back to the input mutable
        let pushAutomatically = Mutable.create false                
        pushAutomatically |> Binding.mutateToFromView bindingSource "PushAutomatically"
        
        // Create a command that will only execute if
        //    1) Our name has changed from the input
        //    2) We're pushing manually
        //    3) We're not executing an async operation currently, and
        //    4) We're valid
        let canExecute = 
            Signal.notEqual lastGoodName source
            |> Signal.both (Signal.not pushAutomatically)
            |> Signal.both bindingSource.IdleTracker
            |> Signal.both bindingSource.Valid
        let okCommand = bindingSource |> Binding.createCommandChecked "OkCommand" canExecute
        
        // Demonstrate an "asynchronous command"
        let asyncCommand = bindingSource |> Binding.createCommandChecked "AsyncCommand" bindingSource.IdleTracker

        // Create a mapping operation - we'll also add asynchronous subscriptions later
        let asyncMapping _ = 
            async {
                do! Async.Sleep 100
                printf "Running..."
                for x in [1 .. 20] do
                    do! Async.Sleep 100
                    printf "."
                
                printfn "Done!"
                return 1
            }
        
        // We need to subscribe *something* to our command
        // In this case, we map (async) then subscribe to the results
        // Since we use "mapAsyncTracked", we disable both commands and editors while this is operating
        asyncCommand
        |> Observable.mapAsyncTracked asyncMapping bindingSource.IdleTracker
        |> Observable.subscribe (fun nv -> printfn "Received %d from command result" nv)
        |> bindingSource.AddDisposable

        let automaticUpdates =
            // To push automatically, we output the signal whenever it's valid as an observable
            // Note that we use FilterValid to guarantee that all validation is completed before
            // the final signal is sent through.  This isn't a problem with the command approach,
            // but guarantees our validation to always be up to date before it's queried in the filter
            userChanges
            |> Observable.filter (fun _ -> pushAutomatically.Value)
        // Command-triggered updates
        let commandUpdates =
            // In this case, we can use our command to map the right value out when it's clicked
            // Since the command already is only enabled when we're valid, we don't need a validity filter here
            okCommand
            |> Observable.filterBy (Signal.not pushAutomatically)             
            |> Observable.map (fun _ -> lastGoodName.Value)

        // Combine our automatic and manual updates into one signal, and push them to the backing observable
        Observable.merge automaticUpdates commandUpdates
        |> bindingSource.OutputObservable

        // Return the binding observable for use as a View Model
        bindingSource

