open System

open FsXaml
open System.Windows

type MainWin = XAML<"MainWindow.xaml">

[<STAThread>]
[<EntryPoint>]
let main _ =     
    Gjallarhorn.Wpf.Platform.install true |> ignore

    let app = Application()
    let win = MainWin(DataContext = Context.create())

    app.Run win
