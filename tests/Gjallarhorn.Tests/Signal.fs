module Gjallarhorn.Tests.Signal

open Gjallarhorn
open Gjallarhorn.Internal
// open Gjallarhorn.View.Operators

open System
open NUnit.Framework

let mutable culture : System.Globalization.CultureInfo = null

[<SetUp>]
let setup () =
    culture <- System.Threading.Thread.CurrentThread.CurrentCulture
    System.Threading.Thread.CurrentThread.CurrentCulture <- System.Globalization.CultureInfo.InvariantCulture

[<TearDown>]
let teardown () =
    System.Threading.Thread.CurrentThread.CurrentCulture <- culture

[<Test;TestCaseSource(typeof<Utilities>,"CasesStart")>]
let ``Signal\constant constructs with proper value`` start =
    let value = Signal.constant start

    Assert.AreEqual(box start, value.Value)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
let ``Signal\copyTo copies across proper values`` start finish =
    let value = Mutable.create start

    let view = Mutable.create Unchecked.defaultof<'a>

    Assert.AreEqual(box Unchecked.defaultof<'a>, view.Value)

    use __ = Signal.Subscription.copyTo view value 

    Assert.AreEqual(box start, value.Value)
    Assert.AreEqual(box start, view.Value)

    value.Value <- finish
    
    Assert.AreEqual(box finish, value.Value)
    Assert.AreEqual(box finish, view.Value)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartToString")>]
let ``Signal\map constructs from mutable`` start finish =
    let value = Mutable.create start
    let view = 
        value 
        |> Signal.map (fun i -> i.ToString()) 

    Assert.AreEqual(box view.Value, finish)

[<Test;ExpectedException>]
let ``Signal\map with exception throws on construction`` () =
    let value = Mutable.create 1
    let view = 
        value 
        |> Signal.map (fun i -> failwith "Bad Case")     
    ()

[<Test;ExpectedException()>]
let ``Signal\map with exception throws on subscription`` () =
    let value = Mutable.create 1
    let view = 
        value 
        |> Signal.map (fun i -> failwith "Bad Case")  
    use _disp = 
        view
        |> Signal.Subscription.create ignore
    ()

[<Test;TestCaseSource(typeof<Utilities>,"CasesPairToString")>]
let ``Signal\map2 constructs from mutables`` start1 start2 finish =
    let v1 = Mutable.create start1
    let v2 = Mutable.create start2
    let map i j = i.ToString() + "," + j.ToString()
    let view = 
        Signal.map2 map v1 v2

    Assert.AreEqual(box view.Value, finish)


[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``Signal updates with mutable`` start initialView finish finalView =
  let result = Mutable.create start
  let view = Signal.map (fun i -> i.ToString()) result

  Assert.AreEqual(initialView, view.Value)

  result.Value <- finish
  Assert.AreEqual(finalView, view.Value)

[<Test;TestCaseSource(typeof<Utilities>,"CasesPairStartEndToStringPairs")>]
let ``Signal\map2 updates from mutables`` start1 start2 startResult finish1 finish2 finishResult =
    let v1 = Mutable.create start1
    let v2 = Mutable.create start2
    let view = Signal.map2 (fun i j -> i.ToString() + "," + j.ToString()) v1 v2

    Assert.AreEqual(box view.Value, startResult)

    v1.Value <- finish1
    v2.Value <- finish2

    Assert.AreEqual(box view.Value, finishResult)


[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``Signal updates with signal`` start initialView finish finalView =
    // Create a mutable value
    let result = Mutable.create start
    
    // Create a view to turn the value from int -> string
    let view = Signal.map (fun i -> i.ToString()) result
    
    // Create a view to turn the first view back from string -> int
    let backView = Signal.map (fun s -> Convert.ChangeType(s, start.GetType())) view
    
    Assert.AreEqual(view.Value, initialView)
    Assert.AreEqual(backView.Value, start)
    
    result.Value <- finish
    Assert.AreEqual(view.Value, finalView)
    Assert.AreEqual(backView.Value, finish)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``Cached View updates with View`` start initialView finish finalView =
    // Create a mutable value
    let result = Mutable.create start
    
    // Create a view to turn the value from int -> string
    let view = Signal.map (fun i -> i.ToString()) result
    
    // Create a view to turn the first view back from string -> int
    let bv = Signal.map (fun s -> Convert.ChangeType(s, start.GetType())) view

    // Cache the view
    let backView = Signal.cache bv
    
    Assert.AreEqual(view.Value, initialView)
    Assert.AreEqual(backView.Value, start)
    
    result.Value <- finish
    Assert.AreEqual(view.Value, finalView)
    Assert.AreEqual(backView.Value, finish)

[<Test>]
let ``Signal\filter doesn't propogate inappropriate changes`` () =
    let v = Mutable.create 1
    let view = Signal.map (fun i -> 10*i) v

    let filter = 
        view 
        |> Signal.filter (fun i -> i < 100) view.Value

    Assert.AreEqual(10, filter.Value)

    v.Value <- 5
    Assert.AreEqual(50, filter.Value)

    v.Value <- 25
    Assert.AreEqual(50, filter.Value)

[<Test>]
let ``Signal\choose doesn't propogate inappropriate changes`` () =
    let v = Mutable.create 1
    let view = Signal.map (fun i -> 10*i) v

    let filter = Signal.choose (fun i -> if i < 100 then Some(i) else None) view.Value view

    Assert.AreEqual(10, filter.Value)

    v.Value <- 5
    Assert.AreEqual(50, filter.Value)

    v.Value <- 25
    Assert.AreEqual(50, filter.Value)   

[<Test>]
let ``Signal\map3 propogates successfully`` () =
    let f = (fun a b c -> sprintf "%d,%d,%f" a b c)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3.0
    
    let view = Signal.map3 f v1 v2 v3

    use sub = Signal.Subscription.create (fun _ -> ()) view
    Assert.AreEqual("1,2,3.000000", view.Value)

    Assert.IsNotNull(sub)

[<Test>]
let ``Signal\map4 propogates successfully`` () =
    let f = (fun a b c d -> sprintf "%d,%d,%f,%d" a b c d)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3.0
    let v4 = Mutable.create 4

    let view = Signal.map4 f v1 v2 v3 v4

    Assert.AreEqual("1,2,3.000000,4", view.Value)

[<Test>]
let ``Signal\map5 propogates successfully`` () =
    let f = (fun a b c d e -> sprintf "%d,%d,%f,%d,%d" a b c d e)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3.0
    let v4 = Mutable.create 4
    let v5 = Mutable.create 5

    let view = Signal.map5 f v1 v2 v3 v4 v5

    Assert.AreEqual("1,2,3.000000,4,5", view.Value)

[<Test>]
let ``Signal\map6 propogates successfully`` () =
    let f = (fun a b c d e f' -> sprintf "%d,%d,%f,%d,%d,%d" a b c d e f')
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3.0
    let v4 = Mutable.create 4
    let v5 = Mutable.create 5
    let v6 = Mutable.create 6

    let view = Signal.map6 f v1 v2 v3 v4 v5 v6

    Assert.AreEqual("1,2,3.000000,4,5,6", view.Value)

[<Test>]
let ``Signal\map10 propogates successfully`` () =
    let f = (fun a b c d e f' g h i j -> sprintf "%d,%d,%f,%d,%d,%d,%f,%d,%d,%d" a b c d e f' g h i j)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3.0
    let v4 = Mutable.create 4
    let v5 = Mutable.create 5
    let v6 = Mutable.create 6
    let v7 = Mutable.create 7.1
    let v8 = Mutable.create 8
    let v9 = Mutable.create 9
    let v10 = Mutable.create 10

    let view = Signal.map10 f v1 v2 v3 v4 v5 v6 v7 v8 v9 v10

    Assert.AreEqual("1,2,3.000000,4,5,6,7.100000,8,9,10", view.Value)

[<Test>]
let ``Signal\map10 notifies properly with input changes`` () =
    let f = (fun a b c d e f' g h i j -> sprintf "%d,%d,%f,%d,%d,%d,%f,%d,%d,%d" a b c d e f' g h i j)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3.0
    let v4 = Mutable.create 4
    let v5 = Mutable.create 5
    let v6 = Mutable.create 6
    let v7 = Mutable.create 7.1
    let v8 = Mutable.create 8
    let v9 = Mutable.create 9
    let v10 = Mutable.create 10

    let view = Signal.map10 f v1 v2 v3 v4 v5 v6 v7 v8 v9 v10

    Assert.AreEqual("1,2,3.000000,4,5,6,7.100000,8,9,10", view.Value)

    let changes = ref 0
    use _disp = Signal.Subscription.create (fun _ -> incr changes) view

    Assert.AreEqual("1,2,3.000000,4,5,6,7.100000,8,9,10", view.Value)
    Assert.AreEqual(0, !changes)
    v1.Value <- 5
    Assert.AreEqual("5,2,3.000000,4,5,6,7.100000,8,9,10", view.Value)
    Assert.AreEqual(1, !changes)
    v3.Value <- 7.0
    Assert.AreEqual("5,2,7.000000,4,5,6,7.100000,8,9,10", view.Value)
    Assert.AreEqual(2, !changes)
    v4.Value <- 8
    Assert.AreEqual("5,2,7.000000,8,5,6,7.100000,8,9,10", view.Value)
    Assert.AreEqual(3, !changes)

    Assert.IsNotNull(_disp)

[<Test>]
let ``Signal\map10 handles subscription tracking properly`` () =
    let f = (fun a b c d e f' g h i j -> sprintf "%d,%d,%f,%d,%d,%d,%f,%d,%d,%d" a b c d e f' g h i j)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3.0
    let v4 = Mutable.create 4
    let v5 = Mutable.create 5
    let v6 = Mutable.create 6
    let v7 = Mutable.create 7.1
    let v8 = Mutable.create 8
    let v9 = Mutable.create 9
    let v10 = Mutable.create 10

    let depChecks : Internal.IDependent [] = [| 
        v1 ; 
        v2 ; 
        v3 ; 
        v4 ; 
        v5 ; 
        v6 ; 
        v7 ; 
        v8 ; 
        v9 ; 
        v10 |]

    let test() =
        let view = Signal.map10 f v1 v2 v3 v4 v5 v6 v7 v8 v9 v10

        depChecks
        |> Array.iter (fun v -> Assert.AreEqual(true, v.HasDependencies))

        let changes = ref 0
        use _disp = Signal.Subscription.create (fun _ -> incr changes) view

        depChecks
        |> Array.iter (fun v -> Assert.AreEqual(true, v.HasDependencies))

        Assert.AreEqual("1,2,3.000000,4,5,6,7.100000,8,9,10", view.Value)
        Assert.AreEqual(0, !changes)
        v1.Value <- 5
        Assert.AreEqual("5,2,3.000000,4,5,6,7.100000,8,9,10", view.Value)
        Assert.AreEqual(1, !changes)

        Assert.IsNotNull(_disp)
        _disp.Dispose();
    
    test()
    GC.Collect()
    GC.WaitForPendingFinalizers()
    
    depChecks
    |> Array.iter (fun v -> Assert.AreEqual(false, v.HasDependencies))


[<Test>]
let ``Signal\map4 notifies properly with input changes`` () =
    let f = (fun a b c d -> sprintf "%d,%d,%d,%d" a b c d)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3
    let v4 = Mutable.create 4
    
    let view = Signal.map4 f v1 v2 v3 v4

    let changes = ref 0
    use _disp = Signal.Subscription.create (fun _ -> incr changes) view

    Assert.AreEqual("1,2,3,4", view.Value)
    Assert.AreEqual(0, !changes)
    v1.Value <- 5
    Assert.AreEqual("5,2,3,4", view.Value)
    Assert.AreEqual(1, !changes)
    v3.Value <- 7
    Assert.AreEqual("5,2,7,4", view.Value)
    Assert.AreEqual(2, !changes)
    v4.Value <- 8
    Assert.AreEqual("5,2,7,8", view.Value)
    Assert.AreEqual(3, !changes)

    Assert.IsNotNull(_disp)
[<Test>]
let ``Signal\map4 preserves tracking`` () =
    let f = (fun a b c d -> sprintf "%d,%d,%d,%d" a b c d)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3
    let v4 = Mutable.create 4
    
    let view = Signal.map4 f v1 v2 v3 v4
    
    // Mutate
    v1.Value <- 5
    v3.Value <- 7
    Assert.AreEqual(view.Value, "5,2,7,4")

[<Test>]
let ``Signal\map without subscription triggers proper number of updates`` () =
    let changes = ref 0
    let x = Mutable.create 0
    let f i = incr changes; i*i
    let y = Signal.map f x // "processing" 
    Assert.AreEqual(1, !changes)
    printfn "x = %d" x.Value // nothing 
    Assert.AreEqual(1, !changes)
    printfn "y = %d" y.Value // nothing 
    Assert.AreEqual(1, !changes)
    printfn "y = %d" y.Value // nothing
    Assert.AreEqual(1, !changes)
    x.Value <- 2 // nothing - by design
    Assert.AreEqual(1, !changes)
    printfn "y = %d" y.Value // "processing" - gets value at first access, since there's no active subscription 
    Assert.AreEqual(2, !changes)
    printfn "y = %d" y.Value // nothing    
    Assert.AreEqual(2, !changes)

[<Test>]
let ``Signal\map with subscription triggers proper number of updates`` () =
    let changes = ref 0
    let x = Mutable.create 0
    let f i = 
        incr changes
        i*i
    let y = Signal.map f x // "processing" 
    use disp = Signal.Subscription.create ignore y 
    Assert.AreEqual(1, !changes)
    printfn "x = %d" x.Value // nothing 
    Assert.AreEqual(1, !changes)
    printfn "y = %d" y.Value // nothing 
    Assert.AreEqual(1, !changes)
    printfn "y = %d" y.Value // nothing
    Assert.AreEqual(1, !changes)
    x.Value <- 2 // triggers    
    Assert.AreEqual(2, !changes)
    printfn "y = %d" y.Value // nothing
    Assert.AreEqual(2, !changes)
    printfn "y = %d" y.Value // nothing    
    Assert.AreEqual(2, !changes)

[<Test>]
let ``Issue #16 - Signal.map evaluations - Without Subscription`` () =
    use sw = new System.IO.StringWriter()

    let x = Mutable.create 0
    let f i = 
        sw.Write("*")
        printfn "processing"
        i*i
    Assert.AreEqual("", sw.ToString())
    let y = Signal.map f x // "processing" 
    Assert.AreEqual("*", sw.ToString())
    printfn "x = %A" x.Value // nothing 
    printfn "y = %A" y.Value // nothing 
    printfn "y = %A" y.Value // nothing

    printfn "Setting value"
    x.Value <- 1 // nothing - by design
    Assert.AreEqual("*", sw.ToString())
    printfn "Before processing"
    printfn "y = %A" y.Value // "processing" - gets value at first access, since there's no active subscription 
    Assert.AreEqual("**", sw.ToString())
    printfn "y = %A" y.Value // nothing
    Assert.AreEqual("**", sw.ToString())

[<Test>]
let ``Issue #16 - Signal.map evaluations - With Subscription`` () =
    use sw = new System.IO.StringWriter()

    let x = Mutable.create 0
    let f i = 
        sw.Write("*")
        printfn "processing"
        i*i
    Assert.AreEqual("", sw.ToString())
    let y = Signal.map f x // "processing" 
    Assert.AreEqual("*", sw.ToString())
    use disp = Signal.Subscription.create ignore y // Add an active subscription - no-op in this case
    printfn "x = %A" x.Value // nothing 
    printfn "y = %A" y.Value // nothing 
    printfn "y = %A" y.Value // nothing
    Assert.AreEqual("*", sw.ToString())
    printfn "Setting value"
    x.Value <- 1 // "processing" - Subscription needs to update immediately
    Assert.AreEqual("**", sw.ToString())
    printfn "After processing"
    printfn "y = %A" y.Value // nothing 
    printfn "y = %A" y.Value // nothing
    Assert.AreEqual("**", sw.ToString())

type Msg =
| Update of int

[<Test>]
let ``Async mutables functions as mutable`` () =
    let update msg state =
        match msg with
        | Update(newValue) -> newValue

    let state = Mutable.createAsync 0

    let m = state :> IMutatable<_>

    Assert.AreEqual(0, m.Value)

    m.Value <- 3
    Assert.AreEqual(3, m.Value)

    Update(5) |> update |> state.Update |> ignore
    Assert.AreEqual(5, m.Value)

[<Test>]
let ``Async mutables propogates changes properly`` () =
    let update msg state =
        match msg with
        | Update(newValue) -> newValue

    let state = Mutable.createAsync 0 

    let m = state :> IMutatable<_>

    let sign = state |> Signal.map (fun v -> v * 10)

    let mutable value = sign.Value

    use __ = sign |> Observable.subscribe (fun v -> value <- v)

    Assert.AreEqual(0, m.Value)

    m.Value <- 3
    Assert.AreEqual(3, m.Value)

    Update(5) |> update |> state.Update |> ignore
    Assert.AreEqual(5, m.Value)

    Assert.AreEqual(50, value)

[<Test>]
let ``Signal\toFunction allows closing over a mutable`` () =
    let mutable result = ""
    let count = ref 0

    let myService config : unit -> string =
      fun () -> 
        incr count
        sprintf "config=%s" config       

    let myConfig = Mutable.create "A"

    let myService' =
        myConfig
        |> Signal.map myService
        |> Signal.toFunction

    Assert.AreEqual(0, !count)
    result <- myService' () 

    Assert.AreEqual(1, !count)
    Assert.AreEqual("config=A", result)

    // Setting the value 
    myConfig.Value <- "B"
    Assert.AreEqual(1, !count)
    
    // But calling it now that the original is set does
    result <- myService' () 
    Assert.AreEqual("config=B", result)
    Assert.AreEqual(2, !count)


[<Test>]
let ``Signal\toFunction allows closing over a mapped signal`` () =
    let mutable result = ""
    let count = ref 0

    let myService (config:string) : unit -> string =
      fun () -> 
        incr count
        sprintf "config=%s" config
        

    let myConfig = Mutable.create 1

    let myService' =
        myConfig
        |> Signal.map string
        |> Signal.map myService
        |> Signal.toFunction

    Assert.AreEqual(0, !count)
    result <- myService' () 

    Assert.AreEqual(1, !count)
    Assert.AreEqual("config=1", result)

    // Setting the value doesn't trigger execution
    myConfig.Value <- 2
    Assert.AreEqual(1, !count)
    
    // But calling it now that the original is set does
    result <- myService' () 
    Assert.AreEqual("config=2", result)
    Assert.AreEqual(2, !count)

[<Test>]
let ``Signal\mapFunction allows closing over a mapped signal`` () =
    let mutable result = ""
    let count = ref 0

    let myService (config:string) : unit -> string =
      fun () -> 
        incr count
        sprintf "config=%s" config
        

    let myConfig = Mutable.create 1

    let myService' =
        myConfig
        |> Signal.map string
        |> Signal.mapFunction myService        

    Assert.AreEqual(0, !count)
    result <- myService' () 

    Assert.AreEqual(1, !count)
    Assert.AreEqual("config=1", result)

    // Setting the value doesn't trigger execution
    myConfig.Value <- 2
    Assert.AreEqual(1, !count)
    
    // But calling it now that the original is set does
    result <- myService' () 
    Assert.AreEqual("config=2", result)
    Assert.AreEqual(2, !count)

[<Test>]
let ``Signal\map which throws is able to be handled`` () =
    try
        let value = Mutable.create 1
        let final = value |> Signal.map (fun _ -> failwith "...")
        value.Value <- 5
        printfn "%A" final.Value
    with
    | _ as exp -> printfn "%A" exp.Message