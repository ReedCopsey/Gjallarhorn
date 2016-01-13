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

[<Test>]
let ``Mutable\filter prevents edits to source`` () =
    let value = Mutable.create 0

    let filtered = Mutable.filter (fun v -> v < 10) value

    Assert.AreEqual(0, value.Value)
    Assert.AreEqual(0, filtered.Value)

    filtered.Value <- 5
    Assert.AreEqual(5, value.Value)
    Assert.AreEqual(5, filtered.Value)

    filtered.Value <- 20
    Assert.AreEqual(5, value.Value)
    Assert.AreEqual(20, filtered.Value)

[<Test>]
let ``Mutable\filter filters edits from propogating from source`` () =
    let value = Mutable.create 0

    let filtered = Mutable.filter (fun v -> v < 10) value

    let changes = ref 0
    use _disp = View.subscribe (fun v -> incr changes) value

    Assert.AreEqual(0, value.Value)
    Assert.AreEqual(0, filtered.Value)
    Assert.AreEqual(0, !changes)

    filtered.Value <- 5
    Assert.AreEqual(5, value.Value)
    Assert.AreEqual(5, filtered.Value)
    Assert.AreEqual(1, !changes)

    filtered.Value <- 20
    Assert.AreEqual(20, filtered.Value)
    Assert.AreEqual(5, value.Value)
    Assert.AreEqual(1, !changes)

    filtered.Value <- 7
    Assert.AreEqual(7, filtered.Value)
    Assert.AreEqual(7, value.Value)
    Assert.AreEqual(2, !changes)

[<Test>]
let ``Mutable\filter prevents notifications from source`` () =
    let value = Mutable.create 0

    let filtered = Mutable.filter (fun v -> v < 10) value

    Assert.AreEqual(0, value.Value)
    Assert.AreEqual(0, filtered.Value)

    filtered.Value <- 5
    Assert.AreEqual(5, value.Value)
    Assert.AreEqual(5, filtered.Value)

    filtered.Value <- 20
    Assert.AreEqual(5, value.Value)
    Assert.AreEqual(20, filtered.Value)
