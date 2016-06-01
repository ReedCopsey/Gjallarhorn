open System

open FsXaml
open System.Windows

type MainWin = XAML<"MainWindow.xaml">

[<STAThread>]
[<EntryPoint>]
let main _ =     
    Gjallarhorn.Wpf.install true |> ignore

    let app = Application()
    let win = MainWin(DataContext = Context.create())

    app.Run win
