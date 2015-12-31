module Gjallarhorn.Tests.Edit

open Gjallarhorn.Control

open System
open NUnit.Framework

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``Edit.map pushes proper edits through to source`` (start : 'a) (stView : string) finish (finishView : string) =
    let value = Mutable.create start
    let conv a : 'T = System.Convert.ChangeType(a, typeof<'T>) :?> 'T
    let editor = Edit.map conv conv value
    Assert.AreEqual(box start, value.Value)
    Assert.AreEqual(stView, editor.Value)

    editor.Value <- finishView
    Assert.AreEqual(box finish, value.Value)
    Assert.AreEqual(finishView, editor.Value)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEndToStringPairs")>]
let ``Edit.mapConvertible pushes proper edits through to source`` (start : 'a) (stView : string) finish (finishView : string) =
    let value = Mutable.create start
    let editor = Edit.mapConvertible value
    Assert.AreEqual(box start, value.Value)
    Assert.AreEqual(stView, editor.Value)

    editor.Value <- finishView
    Assert.AreEqual(box finish, value.Value)
    Assert.AreEqual(finishView, editor.Value)


type TestCase = { A : int ; B : float }

[<Test>]
let ``Edit.step works on record`` () =
    let value = Mutable.create { A = 42 ; B = 23.2 }

    let aEditor = Edit.step (fun tc -> tc.A) (fun tc a -> {tc with A = a}) value
    let bEditor = Edit.step (fun tc -> tc.B) (fun tc b -> {tc with B = b}) value

    aEditor.Value <- 32
    bEditor.Value <- 24.2
    Assert.AreEqual({ A = 32 ; B = 24.2}, value.Value)
