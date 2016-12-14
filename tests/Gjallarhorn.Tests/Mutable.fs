module Gjallarhorn.Tests.Mutable

open Gjallarhorn

open Gjallarhorn.Tests
open System.Threading

open NUnit.Framework


[<Test;TestCaseSource(typeof<Utilities>,"CasesStart")>]
let ``Mutable\create constructs mutable`` start =
    let value = Mutable.create start
    Assert.AreEqual(box start, value.Value)
  

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
let ``Mutable can be mutated`` start finish =
    let value = Mutable.create start
    Assert.AreEqual(box start, value.Value)
    
    value.Value <- finish
    Assert.AreEqual(box finish, value.Value)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
let ``Mutable\get retrieves value`` start finish =
    let value = Mutable.create start
    Assert.AreEqual(box start, Mutable.get value)
    
    Mutable.set value finish
    Assert.AreEqual(box finish, Mutable.get value)
  
[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
let ``Mutable\createAsync get retrieves value`` start finish =
    let value = Mutable.createAsync start
    Assert.AreEqual(box start, Mutable.get value)
    
    Mutable.set value finish
    Assert.AreEqual(box finish, Mutable.get value)
  
  
[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
let ``Mutable\set mutates value`` start finish =
    let value = Mutable.create start
    Assert.AreEqual(box start, box value.Value)
    
    Mutable.set value finish 
    Assert.AreEqual(box finish, box value.Value)

type ValueHolder = { Value : int } 
  
[<Test>]
let ``Mutable\createThreadsafe updates properly`` () =
    let update v = { Value = v.Value + 1 }    
    
    let m = Mutable.create { Value = 0 }
    let ts = Mutable.createThreadsafe { Value = 0 }

    let max = 10000
    let input = [ 0 .. max ]
    
    System.Threading.Tasks.Parallel.ForEach(input, fun _ -> m.Value <- update m.Value)  |> ignore
    System.Threading.Tasks.Parallel.ForEach(input, fun _ -> ts.Update update |> ignore) |> ignore

    printfn "Mutable %d / Threadsafe %d" m.Value.Value ts.Value.Value

    Assert.AreEqual(1 + max, ts.Value.Value)
    // Note that this is often not the same - which is why the print above is nice, but that's not guaranteed
    Assert.GreaterOrEqual(ts.Value.Value, m.Value.Value)

[<Test>]
let ``Mutable\createAsync updates properly`` () =
    let update v = { Value = v.Value + 1 }    
    
    let m = Mutable.create { Value = 0 }
    let ts = Mutable.createAsync { Value = 0 }

    let max = 10000
    let input = [ 0 .. max ]
    
    System.Threading.Tasks.Parallel.ForEach(input, fun _ -> m.Value <- update m.Value)  |> ignore
    System.Threading.Tasks.Parallel.ForEach(input, fun _ -> ts.Update update |> ignore) |> ignore

    printfn "Mutable %d / Threadsafe %d" m.Value.Value ts.Value.Value

    Assert.AreEqual(1 + max, ts.Value.Value)
    // Note that this is often not the same - which is why the print above is nice, but that's not guaranteed
    Assert.GreaterOrEqual(ts.Value.Value, m.Value.Value)

[<Test>]
let ``Mutable\createThreadsafe updates signals properly`` () =
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

    Assert.AreEqual(1 + max, s.Value)
    Assert.AreEqual(1 + max, r)
    // Note that this is often not the same - which is why the print above is nice, but that's not guaranteed
    Assert.GreaterOrEqual(s.Value, m.Value.Value)

[<Test>]
let ``Mutable\createAsync updates signals properly`` () =
    let update v = { Value = v.Value + 1 }    
    
    let mutable r = 0
    let m = Mutable.create { Value = 0 }
    let ts = Mutable.createAsync { Value = 0 }
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

    Assert.AreEqual(1 + max, s.Value)
    Assert.AreEqual(1 + max, r)
    // Note that this is often not the same - which is why the print above is nice, but that's not guaranteed
    Assert.GreaterOrEqual(s.Value, m.Value.Value)
