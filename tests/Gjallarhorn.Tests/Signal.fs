module Gjallarhorn.Tests.Signal

open Gjallarhorn
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
let ``View updates with mutable`` start initialView finish finalView =
  let result = Mutable.create start
  let view = Signal.map (fun i -> i.ToString()) result

  Assert.AreEqual(view.Value, initialView)

  result.Value <- finish
  Assert.AreEqual(view.Value, finalView)

[<Test;TestCaseSource(typeof<Utilities>,"CasesPairStartEndToStringPairs")>]
let ``View2 updates from mutables`` start1 start2 startResult finish1 finish2 finishResult =
    let v1 = Mutable.create start1
    let v2 = Mutable.create start2
    let view = Signal.map2 (fun i j -> i.ToString() + "," + j.ToString()) v1 v2

    Assert.AreEqual(box view.Value, startResult)

    v1.Value <- finish1
    v2.Value <- finish2

    Assert.AreEqual(box view.Value, finishResult)


[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``View updates with view`` start initialView finish finalView =
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

    let depChecks = [| 
        v1 :> IDependent ; 
        v2 :> IDependent ; 
        v3 :> IDependent ; 
        v4 :> IDependent ; 
        v5 :> IDependent ; 
        v6 :> IDependent ; 
        v7 :> IDependent ; 
        v8 :> IDependent ; 
        v9 :> IDependent ; 
        v10 :> IDependent |]

    let view = Signal.map10 f v1 v2 v3 v4 v5 v6 v7 v8 v9 v10

    depChecks
    |> Array.iter (fun v -> Assert.AreEqual(false, v.HasDependencies))

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
