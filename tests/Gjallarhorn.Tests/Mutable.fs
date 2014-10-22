module Gjallarhorn.Tests.Mutable

open Gjallarhorn

open Gjallarhorn.Tests

open NUnit.Framework


[<Test;TestCaseSource(typeof<Utilities>,"CasesStart")>]
let ``Mutable.create constructs mutable`` start =
    let value = Mutable.create start
    Assert.AreEqual(box start, value.Value)
  

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
let ``Mutable can be mutated`` start finish =
    let value = Mutable.create start
    Assert.AreEqual(box start, value.Value)
    
    value.Value <- finish
    Assert.AreEqual(box finish, value.Value)

[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
let ``Mutable.get retrieves value`` start finish =
    let value = Mutable.create start
    Assert.AreEqual(box start, Mutable.get value)
    
    Mutable.set value finish
    Assert.AreEqual(box finish, Mutable.get value)
  
  
[<Test;TestCaseSource(typeof<Utilities>,"CasesStartEnd")>]
let ``Mutable.set mutates value`` start finish =
    let value = Mutable.create start
    Assert.AreEqual(box start, box value.Value)
    
    Mutable.set value finish
    Assert.AreEqual(box finish, box value.Value)
  
