open System
open FsXaml

open ViewModels 
open Gjallarhorn
type App = XAML<"App.xaml">

// Install WPF Binding targets
Gjallarhorn.Wpf.install()

[<STAThread>]
[<EntryPoint>]
let main _ = 
    // Create our "source" that will get updated
    let name = Mutable.create { First = "" ; Last = "" }

    // Prints model updates to console
    use _sub = name |> Observable.subscribe (fun n -> printfn "Name in \"model\" updated to [%s %s]" n.First n.Last) 

    let vm, updates = VM.createMainViewModel name

    // Copy updates out of our vm into our model
    // This could easily track history, etc, if desired
    use _sub2 = updates |> Signal.copyTo name    

    let window = Views.MainWindow().Root
    window.DataContext <- vm
    App().Root.Run(window)
