open System

open FsXaml
open Gjallarhorn
open Gjallarhorn.Bindable
open System.Windows

// -------- Model ----------
// Model is a simple integer for counter
type Model = int
let initModel i : Model = i

// -------- Update ----------
type Msg = 
    | Increment 
    | Decrement

let update msg (model : Model) =
    match msg with
    | Increment -> model + 1
    | Decrement -> model - 1

// -------- View ----------
let viewContext (model : ISignal<Model>) =    
    let source = Binding.createObservableSource()

    // Create a property to display our current value    
    Binding.toView source "Current" model

    // Create commands for our buttons
    [
        Binding.createMessage "Increment" Increment source
        Binding.createMessage "Decrement" Decrement source
    ]
    |> source.OutputObservables 

    source

type MainWin = XAML<"MainWindow.xaml">

[<STAThread>]
[<EntryPoint>]
let main _ =     
    // Install the WPF platform bindings
    Gjallarhorn.Wpf.Platform.install true |> ignore

    // We'll hold our state as a mutable, but anything that could be converted to a signal would work
    let state = Mutable.create <| initModel 5

    // Map our state directly into the view context - this gives us something that can be data bound
    let viewContext = viewContext state

    // The context is also IObservable<Msg> in this case, so handle messages coming from the view, and update...
    use _sub = viewContext.Subscribe (fun msg -> state.Value <- update msg state.Value)

    // Create our window, set the data context, and run
    let win = MainWin(DataContext = viewContext)

    // Create and run our application
    Application().Run win
