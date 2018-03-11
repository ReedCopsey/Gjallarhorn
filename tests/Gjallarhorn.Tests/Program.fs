module Main
open Expecto

[<EntryPoint>]
let main args = 
    let config = { defaultConfig with ``parallel`` = true }
    runTestsWithArgs config [| "--summary" |] Tests.tests