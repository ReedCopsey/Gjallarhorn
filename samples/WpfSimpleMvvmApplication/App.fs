open System
open FsXaml

open ViewModels 
type App = XAML<"App.xaml">

[<STAThread>]
[<EntryPoint>]
let main _ = 
    Gjallarhorn.Wpf.install()

    let window = Views.MainWindow().Root
    window.DataContext <- VM.createMain()
    App().Root.Run(window)
