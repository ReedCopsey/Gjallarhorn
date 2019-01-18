module Mutable

open Gjallarhorn
open Expecto

type ValueHolder = { Value : int } 

let tests =
  testList "Mutable" [

        testProperty "create constructs mutable" <| fun v ->
            let value = Mutable.create v
            Expect.equal value.Value v "should be equal"
            
        testProperty "can be mutated" <| fun start finish ->
            let value = Mutable.create start
            Expect.equal value.Value start "should be equal"

            value.Value <- finish 
            Expect.equal value.Value finish "should be equal"

        testProperty "get retrieves value" <| fun start finish ->
            let value = Mutable.create start
            Expect.equal (Mutable.get value) start "should be equal" 

            Mutable.set value finish
            Expect.equal (Mutable.get value) finish "should be equal"


        testProperty "set mutates value" <| fun start finish ->
            let value = Mutable.create start
            Expect.equal value.Value start "should be equal"

            Mutable.set value finish
            Expect.equal value.Value finish "should be equal"


        testProperty "createAsync get retrieves value" <| fun start finish ->
            let value = Mutable.createAsync start
            Expect.equal (Mutable.get value) start "should be equal" 
    
            Mutable.set value finish
            Expect.equal (Mutable.get value) finish "should be equal" 

        testCase "createThreadsafe updates properly" <| fun _ ->

            let update v = { Value = v.Value + 1 }  
    
            let m = Mutable.create { Value = 0 }
            let ts = Mutable.createThreadsafe { Value = 0 }

            let max = 10000
            let input = [ 0 .. max ]
    
            System.Threading.Tasks.Parallel.ForEach(input, fun _ -> m.Value <- update m.Value)  |> ignore
            System.Threading.Tasks.Parallel.ForEach(input, fun _ -> ts.Update update |> ignore) |> ignore

            printfn "Mutable %d / Threadsafe %d" m.Value.Value ts.Value.Value

            Expect.equal ts.Value.Value (1 + max) "should be equal"
            // Note that this is often not the same - which is why the print above is nice, but that's not guaranteed
            Expect.isGreaterThanOrEqual ts.Value.Value m.Value.Value ""

        testCase "createAsync updates properly" <| fun _ ->

            let update v = { Value = v.Value + 1 }  
    
            let m = Mutable.create { Value = 0 }
            let asc = Mutable.createAsync { Value = 0 }

            let max = 10000
            let input = [ 0 .. max ]
    
            System.Threading.Tasks.Parallel.ForEach(input, fun _ -> m.Value <- update m.Value)  |> ignore
            System.Threading.Tasks.Parallel.ForEach(input, fun _ -> asc.Update update |> ignore) |> ignore

            printfn "Mutable %d / Async %d" m.Value.Value asc.Value.Value

            Expect.equal asc.Value.Value (1 + max) "should be equal"
            // Note that this is often not the same - which is why the print above is nice, but that's not guaranteed
            Expect.isGreaterThanOrEqual asc.Value.Value m.Value.Value ""

        testCase "createThreadsafe updates signals properly" <| fun _ ->

            let update v = { Value = v.Value + 1 }    
    
            let mutable r = 0
            let m = Mutable.create { Value = 0 }
            let ts = Mutable.createThreadsafe { Value = 0 }
            let s = ts |> Signal.map (fun v -> v.Value)    

            let o = obj()
            use _s = 
                s         
                |> Observable.subscribe (fun v -> lock o (fun _ -> r <- max r v))

            let max = 10000
            let input = [ 0 .. max ]
    
            System.Threading.Tasks.Parallel.ForEach(input, fun _ -> m.Value <- update m.Value)  |> ignore
            System.Threading.Tasks.Parallel.ForEach(input, fun _ -> ts.Update update |> ignore) |> ignore

            printfn "Mutable %d / Threadsafe %d" m.Value.Value ts.Value.Value

            Expect.equal s.Value (1 + max) "should be equal"
            Expect.equal r (1 + max) "should be equal"
            
            // Note that this is often not the same - which is why the print above is nice, but that's not guaranteed
            Expect.isGreaterThanOrEqual s.Value m.Value.Value ""

        testCase "createAsync updates signals properly" <| fun _ ->

            let update v = { Value = v.Value + 1 }    
    
            let mutable r = 0
            let m = Mutable.create { Value = 0 }
            let asc = Mutable.createAsync { Value = 0 }
            let s = asc |> Signal.map (fun v -> v.Value)    
    
            let o = obj()
            use _s = 
                s         
                |> Observable.subscribe (fun v -> lock o (fun _ -> r <- max r v))

            let max = 10000
            let input = [ 0 .. max ]
    
            System.Threading.Tasks.Parallel.ForEach(input, fun _ -> m.Value <- update m.Value)  |> ignore
            System.Threading.Tasks.Parallel.ForEach(input, fun _ -> asc.Update update |> ignore) |> ignore

            printfn "Mutable %d / Async %d" m.Value.Value asc.Value.Value

            Expect.equal s.Value (1 + max) "should be equal"
            Expect.equal r (1 + max) "should be equal"
            
            // Note that this is often not the same - which is why the print above is nice, but that's not guaranteed
            Expect.isGreaterThanOrEqual s.Value m.Value.Value ""

  ]