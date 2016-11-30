open System

open ElmInspiredOne

open FsXaml
open Gjallarhorn.Wpf

// The WPF Platform specific bits of this application need to do 2 things:
// 1) They create the view (the actual Window)
// 2) The start the WPF specific version of the framework with the view

// ----------------------------------     View      ---------------------------------- 
// Our platform specific view type
type MainWin = XAML<"MainWindow.xaml">

// ----------------------------------  Application  ---------------------------------- 
[<STAThread>]
[<EntryPoint>]
let main _ =         
    // Run using the WPF wrappers around the basic application framework        
    Framework.runApplication System.Windows.Application MainWin Program.applicationCore