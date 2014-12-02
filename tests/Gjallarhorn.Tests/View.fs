module Gjallarhorn.Tests.View

open Gjallarhorn.Control

open System
open NUnit.Framework

[<Test;TestCaseSource(typeof<Utilities>,"CasesStart")>]
let ``View.constant constructs with proper value`` start =
    let value = View.constant start

    Assert.AreEqual(box start, value.Value)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartToString")>]
let ``View.map constructs from mutable`` start finish =
    let value = Mutable.create start
    let view = 
        value 
        |> View.map (fun i -> i.ToString()) 

    Assert.AreEqual(box view.Value, finish)

[<Test;TestCaseSource(typeof<Utilities>,"CasesPairToString")>]
let ``View.map2 constructs from mutables`` start1 start2 finish =
    let v1 = Mutable.create start1
    let v2 = Mutable.create start2
    let map i j = i.ToString() + "," + j.ToString()
    let view = 
        View.map2 map v1 v2

    Assert.AreEqual(box view.Value, finish)


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
let ``View.filter doesn't propogate inappropriate changes`` () =
    let v = Mutable.create 1
    let view = View.map (fun i -> 10*i) v

    let filter = View.filter (fun i -> i < 100) view

    Assert.AreEqual(10, filter.Value)

    v.Value <- 5
    Assert.AreEqual(50, filter.Value)

    v.Value <- 25
    Assert.AreEqual(50, filter.Value)

[<Test>]
let ``View.choose doesn't propogate inappropriate changes`` () =
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
    
    let view = View.constant f <*> v1 <*> v2 <*> v3 <*> v4

    Assert.AreEqual(view.Value, "1,2,3,4")

[<Test>]
let ``Operator <*> preserves tracking`` () =
    let f = (fun a b c d -> sprintf "%d,%d,%d,%d" a b c d)
    let v1 = Mutable.create 1
    let v2 = Mutable.create 2
    let v3 = Mutable.create 3
    let v4 = Mutable.create 4
    
    let view = View.constant f <*> v1 <*> v2 <*> v3 <*> v4
    // let view = View.apply( View.apply( View.apply( View.apply (View.constant f) v1) v2) v3) v4
    
    // Mutate
    v1.Value <- 5
    v3.Value <- 7
    Assert.AreEqual(view.Value, "5,2,7,4")

[<Test>]
let ``Compose a view using a computation expression``() =
    let m1 = Mutable.create "Foo"
    let m2 = Mutable.create "Bar"

    let view = View.compose {
        let! first = m1
        let! last = m2
        return sprintf "%s %s" first last
    }
    
    Assert.AreEqual("Foo Bar", view.Value)

    // Mutate
    m2.Value <- "Baz"
    Assert.AreEqual("Foo Baz", view.Value)

[<Test>]
let ``Compose a filtered view using a computation expression``() =
    let m1 = Mutable.create 1
    let m2 = Mutable.create 2

    let v1 = m1 |> View.map (fun i -> i+10) 
    let v2 = m2 |> View.map (fun i -> i*100) 

    let view = View.compose {
        let! start = v1
        let! finish = v2
        let! mut = m1
        if finish > 500 then
            return sprintf "%i" start
        else
            return sprintf "%i" (start + finish + mut)
    }

    let toNum = View.map Int32.Parse view
    
    Assert.AreEqual(212, toNum.Value)

    // Mutate
    m1.Value <- 5
    Assert.AreEqual(220, toNum.Value)

    m2.Value <- 7
    Assert.AreEqual(15, toNum.Value)