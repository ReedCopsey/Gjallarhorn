module Tests

open Expecto

[<Tests>]
let tests =
    testList "All Tests" [
        Mutable.tests
    ]

[<EntryPoint>]
let main _ = 0