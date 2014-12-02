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
