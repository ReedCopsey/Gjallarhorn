
open System

open CollectionSample
open CollectionSample.RequestModel
open Gjallarhorn.Wpf

open Views

// The WPF Platform specific bits of this application need to do 2 things:
// 1) They create the view (the actual Window)
// 2) The start the WPF specific version of the framework with the view


// ----------------------------------  Application  ---------------------------------- 
[<STAThread>]
[<EntryPoint>]
let main _ =  
    // These are our "handlers" for accepted and rejected requests
    // We're defining them here to show that we can pass them around from 
    // anywhere in the program, and inject them into the PCL target safely 
    // (since printfn/Console isn't available in PCL)
    // In a "real program" this would likely call out to a service
    let fnAccepted req = 
        Console.ForegroundColor <- ConsoleColor.Green
        printfn "Accepted Request: %A" req.Id
    let fnRejected req = 
        Console.ForegroundColor <- ConsoleColor.Red
        printfn "Rejected Request: %A" req.Id

    // Run using the WPF wrappers around the basic application framework    
    Framework.runApplication App MainWin (Program.applicationCore fnAccepted fnRejected)