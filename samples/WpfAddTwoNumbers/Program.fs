open System

open FsXaml
open FsXaml.Wpf
open System.Windows

type MainWin = XAML<"MainWindow.xaml">

[<STAThread>]
[<EntryPoint>]
let main _ =     
    Gjallarhorn.Wpf.install true |> ignore

    let app = Application()
    let win = MainWin()

    let context = Context.create { One = 42 ; Two = 54 }   
    win.DataContext <- context

    // This works because we created a binding subject, not a binding target.
    // Subjects are observables of the "output" of the binding context
    context.Add (fun v -> printfn "Value updated to %d" v)

    app.Run win
