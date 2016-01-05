module Gjallarhorn.Tests.Mutable

open Gjallarhorn

open Gjallarhorn.Tests

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
let ``Mutable\set mutates value`` start finish =
    let value = Mutable.create start
    Assert.AreEqual(box start, box value.Value)
    
    Mutable.set value finish
    Assert.AreEqual(box finish, box value.Value)
  

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``Mutable\map pushes proper edits through to source`` (start : 'a) (stView : string) finish (finishView : string) =
    let value = Mutable.create start
    let conv a : 'T = System.Convert.ChangeType(a, typeof<'T>) :?> 'T
    let editor = Mutable.map conv conv value
    Assert.AreEqual(box start, value.Value)
    Assert.AreEqual(stView, editor.Value)

    editor.Value <- finishView
    Assert.AreEqual(box finish, value.Value)
    Assert.AreEqual(finishView, editor.Value)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``Mutable\mapConvertible pushes proper edits through to source`` (start : 'a) (stView : string) finish (finishView : string) =
    let value = Mutable.create start
    let editor = Mutable.mapConvertible value
    Assert.AreEqual(box start, value.Value)
    Assert.AreEqual(stView, editor.Value)

    editor.Value <- finishView
    Assert.AreEqual(box finish, value.Value)
    Assert.AreEqual(finishView, editor.Value)


type TestCase = { A : int ; B : float }

[<Test>]
let ``Mutable\step works on record`` () =
    let value = Mutable.create { A = 42 ; B = 23.2 }

    let aEditor = Mutable.step (fun tc -> tc.A) (fun tc a -> {tc with A = a}) value
    let bEditor = Mutable.step (fun tc -> tc.B) (fun tc b -> {tc with B = b}) value

    aEditor.Value <- 32
    bEditor.Value <- 24.2
    Assert.AreEqual({ A = 32 ; B = 24.2}, value.Value)
