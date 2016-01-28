module Gjallarhorn.Tests.View

open Gjallarhorn
open Gjallarhorn.View.Operators

open System
open NUnit.Framework

[<Test;TestCaseSource(typeof<Utilities>,"CasesStart")>]
let ``View\constant constructs with proper value`` start =
    let value = View.constant start

    Assert.AreEqual(box start, value.Value)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
let ``View\copyTo copies across proper values`` start finish =
    let value = Mutable.create start

    let view = Mutable.create Unchecked.defaultof<'a>

    Assert.AreEqual(box Unchecked.defaultof<'a>, view.Value)

    use __ = View.copyTo view value 

    Assert.AreEqual(box start, value.Value)
    Assert.AreEqual(box start, view.Value)

    value.Value <- finish
    
    Assert.AreEqual(box finish, value.Value)
    Assert.AreEqual(box finish, view.Value)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartToString")>]
let ``View\map constructs from mutable`` start finish =
    let value = Mutable.create start
    let view = 
        value 
        |> View.map (fun i -> i.ToString()) 

    Assert.AreEqual(box view.Value, finish)

[<Test;TestCaseSource(typeof<Utilities>,"CasesPairToString")>]
let ``View\map2 constructs from mutables`` start1 start2 finish =
    let v1 = Mutable.create start1
    let v2 = Mutable.create start2
    let map i j = i.ToString() + "," + j.ToString()
    let view = 
        View.map2 map v1 v2

    Assert.AreEqual(box view.Value, finish)

[<Test;TestCaseSource(typeof<Utilities>,"CasesPairToString")>]
let ``View\lift2 matches map2`` start1 start2 finish =
    let v1 = Mutable.create start1
    let v2 = Mutable.create start2
    let map i j = i.ToString() + "," + j.ToString()
    let view1 = 
        View.map2 map v1 v2

    let view2 =
        View.lift2 map v1 v2

    Assert.AreEqual(box view1.Value, finish)
    Assert.AreEqual(box view2.Value, finish)


[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``View updates with mutable`` start initialView finish finalView =
  let result = Mutable.create start
  let view = View.map (fun i -> i.ToString()) result

  Assert.AreEqual(view.Value, initialView)

  result.Value <- finish
  Assert.AreEqual(view.Value, finalView)

[<Test;TestCaseSource(typeof<Utilities>,"CasesPairStartEndToStringPairs")>]
let ``View2 updates from mutables`` start1 start2 startResult finish1 finish2 finishResult =
    let v1 = Mutable.create start1
    let v2 = Mutable.create start2
    let view = View.map2 (fun i j -> i.ToString() + "," + j.ToString()) v1 v2

    Assert.AreEqual(box view.Value, startResult)

    v1.Value <- finish1
    v2.Value <- finish2

    Assert.AreEqual(box view.Value, finishResult)


[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``View updates with view`` start initialView finish finalView =
    // Create a mutable value
    let result = Mutable.create start
    
    // Create a view to turn the value from int -> string
    let view = View.map (fun i -> i.ToString()) result
    
    // Create a view to turn the first view back from string -> int
    let backView = View.map (fun s -> Convert.ChangeType(s, start.GetType())) view
    
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
    let view = View.map (fun i -> i.ToString()) result
    
    // Create a view to turn the first view back from string -> int
    let bv = View.map (fun s -> Convert.ChangeType(s, start.GetType())) view

    // Cache the view
    let backView = View.cache bv
    
    Assert.AreEqual(view.Value, initialView)
    Assert.AreEqual(backView.Value, start)
    
    result.Value <- finish
    Assert.AreEqual(view.Value, finalView)
    Assert.AreEqual(backView.Value, finish)

[<Test>]
let ``View\filter doesn't propogate inappropriate changes`` () =
    let v = Mutable.create 1
    let view = View.map (fun i -> 10*i) v

    let filter = View.filter (fun i -> i < 100) view

    Assert.AreEqual(10, filter.Value)

    v.Value <- 5
    Assert.AreEqual(50, filter.Value)

    v.Value <- 25
    Assert.AreEqual(50, filter.Value)

[<Test>]
let ``View\choose doesn't propogate inappropriate changes`` () =
    let v = Mutable.create 1
    let view = View.map (fun i -> 10*i) v

    let filter = View.choose (fun i -> if i < 100 then Some(i) else None) view

    Assert.AreEqual(10, filter.Value)

    v.Value <- 5
    Assert.AreEqual(50, filter.Value)

    v.Value <- 25
    Assert.AreEqual(50, filter.Value)
    
[<Test>]
let ``Operator <*> allows arbitrary arity`` () =
    let f = (fun a b c d -> sprintf "%d,%d,%d,%d" a b c d)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3
    let v4 = Mutable.create 4
    
    let view = View.pure' f <*> v1 <*> v2 <*> v3 <*> v4

    Assert.AreEqual(view.Value, "1,2,3,4")

[<Test>]
let ``View\lift2 matches operator <!> and <*>`` () =
    let f = (fun a b -> sprintf "%d,%d" a b)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    
    let view1 = f <!> v1 <*> v2 
    let view2 = View.lift2 f v1 v2

    Assert.AreEqual("1,2", view1.Value)
    Assert.AreEqual("1,2", view2.Value)

[<Test>]
let ``View\lift3 matches operator <!> and <*>`` () =
    let f = (fun a b c -> sprintf "%d,%d,%f" a b c)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3.0
    
    let view1 = f <!> v1 <*> v2 <*> v3 
    let view2 = View.lift3 f v1 v2 v3

    Assert.AreEqual("1,2,3.000000", view1.Value)
    Assert.AreEqual("1,2,3.000000", view2.Value)

[<Test>]
let ``View\lift4 matches operator <!> and <*>`` () =
    let f = (fun a b c d -> sprintf "%d,%d,%f,%d" a b c d)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3.0
    let v4 = Mutable.create 4

    let view1 = f <!> v1 <*> v2 <*> v3 <*> v4
    let view2 = View.lift4 f v1 v2 v3 v4

    Assert.AreEqual("1,2,3.000000,4", view1.Value)
    Assert.AreEqual("1,2,3.000000,4", view2.Value)

[<Test>]
let ``View\lift5 matches operator <!> and <*>`` () =
    let f = (fun a b c d e -> sprintf "%d,%d,%f,%d,%d" a b c d e)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3.0
    let v4 = Mutable.create 4
    let v5 = Mutable.create 5

    let view1 = f <!> v1 <*> v2 <*> v3 <*> v4 <*> v5
    let view2 = View.lift5 f v1 v2 v3 v4 v5

    Assert.AreEqual("1,2,3.000000,4,5", view1.Value)
    Assert.AreEqual("1,2,3.000000,4,5", view2.Value)

[<Test>]
let ``View\lift6 matches operator <!> and <*>`` () =
    let f = (fun a b c d e f' -> sprintf "%d,%d,%f,%d,%d,%d" a b c d e f')
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3.0
    let v4 = Mutable.create 4
    let v5 = Mutable.create 5
    let v6 = Mutable.create 6

    let view1 = f <!> v1 <*> v2 <*> v3 <*> v4 <*> v5 <*> v6
    let view2 = View.lift6 f v1 v2 v3 v4 v5 v6

    Assert.AreEqual("1,2,3.000000,4,5,6", view1.Value)
    Assert.AreEqual("1,2,3.000000,4,5,6", view2.Value)

// TODO: Figure out why this is stack overflowing!!!
// [<Test>]
let ``Operator <*> notifies properly with input changes`` () =
    let f = (fun a b c d -> sprintf "%d,%d,%d,%d" a b c d)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3
    let v4 = Mutable.create 4
    
    let view = View.pure' f <*> v1 <*> v2 <*> v3 <*> v4

    let changes = ref 0
    use _disp = View.subscribe (fun v -> incr changes) view

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

[<Test>]
let ``Operator <*> preserves tracking`` () =
    let f = (fun a b c d -> sprintf "%d,%d,%d,%d" a b c d)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3
    let v4 = Mutable.create 4
    
    let view = View.pure' f <*> v1 <*> v2 <*> v3 <*> v4
    // let view = View.apply( View.apply( View.apply( View.apply (View.pure' f) v1) v2) v3) v4
    
    // Mutate
    v1.Value <- 5
    v3.Value <- 7
    Assert.AreEqual(view.Value, "5,2,7,4")

[<Test>]
let ``Operators <!> and <*> preserves tracking`` () =
    let f = (fun a b c d -> sprintf "%d,%d,%d,%d" a b c d)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3
    let v4 = Mutable.create 4
    
    let view = f <!> v1 <*> v2 <*> v3 <*> v4
    // let view = View.apply( View.apply( View.apply( View.apply (View.pure' f) v1) v2) v3) v4
    
    // Mutate
    v1.Value <- 5
    v3.Value <- 7
    Assert.AreEqual(view.Value, "5,2,7,4")
