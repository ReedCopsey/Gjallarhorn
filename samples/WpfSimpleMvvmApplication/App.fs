open System

open Microsoft.FSharp.Control

open FsXaml

open ViewModels 
open Gjallarhorn
type App = XAML<"App.xaml">

// Our "Model" - in this case, a set of history where we can get the current value, add a new one, or fetch the list
module Model = 
    // Create our history as an empty list we'll update
    let nameHistory = Mutable.create ([] : NameModel list) 

    // A stream of the newest name as they come through
    let newNameStream = Observable.map List.head nameHistory

    // Returns the current name
    let current () = 
        match nameHistory.Value with
        | [] -> None
        | head::_ -> Some head

    let add name =
        nameHistory
        |> Mutable.step (fun l -> name :: l)

[<STAThread>]
[<EntryPoint>]
let main _ = 
    // Install WPF Binding targets (and syncronizationContext
    // The context is only required here to allow our async workflow to put ourselves onto the UI thread properly
    let uiContext = Gjallarhorn.Wpf.install true

    // Create application (before other windows) - this allows application-wide sytles to exist when other objects are created
    let app = App().Root

    // This simulates stuff happening outside of the GUI - 
    // Note that the UI updates every 5 seconds automatically
    let rec backgroundUpdates () = 
        async {
            do! Async.Sleep 10000
            do! Async.SwitchToContext uiContext

            let current = Model.current()
            match current with
            | None -> 
                Model.add {First = "Too" ; Last = "Slow" }
            | Some name ->
                let crazyName = { name with First = "a" + name.First }
                Model.add crazyName

            backgroundUpdates()
        }
        |> Async.Start
    backgroundUpdates()

    // Subscribe to do prints to the console when history updates
    use _sub = Model.nameHistory |> Observable.subscribe (fun n -> printfn "Names in \"model\" [%d]: Recent [%s %s]" (List.length n) n.Head.First n.Head.Last)

    // Create our viewmodel
    let vm = VM.createMainViewModel Model.newNameStream {First = "" ; Last = ""}

    // Copy updates out of our vm into our model
    // This could easily track history, etc, if desired
    use _sub2 = vm |> Observable.subscribe Model.add

    let window = Views.MainWindow().Root
    window.DataContext <- vm
    app.Run window
