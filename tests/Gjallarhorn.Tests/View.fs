module Gjallarhorn.Tests.View

open Gjallarhorn

open System
open NUnit.Framework

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartToString")>]
let ``View.create constructs from mutable`` start finish =
    let value = Mutable.create start
    let view = View.create value (fun i -> i.ToString())

    Assert.AreEqual(box view.Value, finish)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``View updates with mutable`` start initialView finish finalView =
  let result = Mutable.create start
  let view = View.create result (fun i -> i.ToString())

  Assert.AreEqual(view.Value, initialView)

  result.Value <- finish
  Assert.AreEqual(view.Value, finalView)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``View updates with view`` start initialView finish finalView =
    // Create a mutable value
    let result = Mutable.create start
    
    // Create a view to turn the value from int -> string
    let view = View.create result (fun i -> i.ToString())
    
    // Create a view to turn the first view back from string -> int
    let backView = View.create view (fun s -> Convert.ChangeType(s, start.GetType()))
    
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
    let view = View.create result (fun i -> i.ToString())
    
    // Create a view to turn the first view back from string -> int
    let bv = View.create view (fun s -> Convert.ChangeType(s, start.GetType()))

    // Cache the view
    let backView = View.createCached bv
    
    Assert.AreEqual(view.Value, initialView)
    Assert.AreEqual(backView.Value, start)
    
    result.Value <- finish
    Assert.AreEqual(view.Value, finalView)
    Assert.AreEqual(backView.Value, finish)
