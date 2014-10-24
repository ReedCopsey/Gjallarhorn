module Gjallarhorn.Tests.View

open Gjallarhorn

open System
open NUnit.Framework

[<Test;TestCaseSource(typeof<Utilities>,"CasesStart")>]
let ``View.constant constructs with proper value`` start =
    let value = View.constant start

    Assert.AreEqual(box start, value.Value)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartToString")>]
let ``View.map constructs from mutable`` start finish =
    let value = Mutable.create start
    let view = View.map value (fun i -> i.ToString())

    Assert.AreEqual(box view.Value, finish)

[<Test;TestCaseSource(typeof<Utilities>,"CasesPairToString")>]
let ``View.map2 constructs from mutables`` start1 start2 finish =
    let v1 = Mutable.create start1
    let v2 = Mutable.create start2
    let view = View.map2 v1 v2 (fun i j -> i.ToString() + "," + j.ToString())

    Assert.AreEqual(box view.Value, finish)


[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``View updates with mutable`` start initialView finish finalView =
  let result = Mutable.create start
  let view = View.map result (fun i -> i.ToString())

  Assert.AreEqual(view.Value, initialView)

  result.Value <- finish
  Assert.AreEqual(view.Value, finalView)

[<Test;TestCaseSource(typeof<Utilities>,"CasesPairStartEndToStringPairs")>]
let ``View2 updates from mutables`` start1 start2 startResult finish1 finish2 finishResult =
    let v1 = Mutable.create start1
    let v2 = Mutable.create start2
    let view = View.map2 v1 v2 (fun i j -> i.ToString() + "," + j.ToString())

    Assert.AreEqual(box view.Value, startResult)

    v1.Value <- finish1
    v2.Value <- finish2

    Assert.AreEqual(box view.Value, finishResult)


[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``View updates with view`` start initialView finish finalView =
    // Create a mutable value
    let result = Mutable.create start
    
    // Create a view to turn the value from int -> string
    let view = View.map result (fun i -> i.ToString())
    
    // Create a view to turn the first view back from string -> int
    let backView = View.map view (fun s -> Convert.ChangeType(s, start.GetType()))
    
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
    let view = View.map result (fun i -> i.ToString())
    
    // Create a view to turn the first view back from string -> int
    let bv = View.map view (fun s -> Convert.ChangeType(s, start.GetType()))

    // Cache the view
    let backView = View.cache bv
    
    Assert.AreEqual(view.Value, initialView)
    Assert.AreEqual(backView.Value, start)
    
    result.Value <- finish
    Assert.AreEqual(view.Value, finalView)
    Assert.AreEqual(backView.Value, finish)
