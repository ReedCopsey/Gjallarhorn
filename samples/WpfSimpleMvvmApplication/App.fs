open System
open FsXaml

open ViewModels 
open Gjallarhorn
type App = XAML<"App.xaml">

[<STAThread>]
[<EntryPoint>]
let main _ = 
    // Install WPF Binding targets
    Gjallarhorn.Wpf.install()

    // Create our "source" that will get updated
    let name = Mutable.create { First = "" ; Last = "" }

    // Print out changes to our model as they come in.    
    name
    |> Signal.subscribe (fun n -> printfn "Name updated to %A" n)
    |> ignore

    let window = Views.MainWindow().Root
    window.DataContext <- VM.createMain name
    App().Root.Run(window)
