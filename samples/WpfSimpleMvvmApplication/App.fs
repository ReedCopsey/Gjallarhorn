open System
open FsXaml

type App = XAML<"App.xaml">

[<STAThread>]
[<EntryPoint>]
let main argv = 
    let window = Views.MainWindow().Root
    window.DataContext <- ViewModels.createMain()
    App().Root.Run(window)
