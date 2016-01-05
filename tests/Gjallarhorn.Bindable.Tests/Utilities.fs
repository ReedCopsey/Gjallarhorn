module Gjallarhorn.Bindable.Tests.Utilities

open Gjallarhorn

open Gjallarhorn.Bindable

open NUnit.Framework

[<Test>]
let ``Utilities\castAs returns None with null`` () =
    let v : System.Type = null

    let test = castAs<System.Type>(v)
    Assert.AreEqual(None, test)

[<Test>]
let ``Utilities\castAs returns None with mismatched type`` () =
    let v : System.Type = typeof<Gjallarhorn.IMutatable<int>>

    let test = castAs<System.Action>(v)
    Assert.IsNotNull(v)
    Assert.AreEqual(None, test)

[<Test>]
let ``Utilities\castAs returns Some with boxed object`` () =
    let v = System.Action<int>(fun _ -> ())
    let boxed = box v
    let test = castAs<System.Action<int>>(boxed)
    Assert.IsNotNull(v)
    Assert.AreEqual(true, test.IsSome)

[<AllowNullLiteral>] 
type Test1() =
    member __.Test with get() = ""

    member __.TestMethod() = ()

[<AllowNullLiteral>] 
type Test2() =
    inherit Test1()

type Test3() =
    do ()

[<Test>]
let ``Utilities\castAs returns Some with boxed subclass`` () =
    let v = Test2()
    let boxed = box v
    let test = castAs<Test1>(boxed)
    Assert.IsNotNull(v)
    Assert.AreEqual(true, test.IsSome)

[<Test>]
let ``Utilities\downcastAndCreateOption returns Some with subclass`` () =
    let v = Test2()
    let test = downcastAndCreateOption<Test1>(v)
    Assert.IsNotNull(v)
    Assert.AreEqual(true, test.IsSome)

[<Test>]
let ``Utilities\downcastAndCreateOption returns Some with id`` () =
    let v = Test1()
    let test = downcastAndCreateOption<Test1>(v)
    Assert.AreEqual(true, test.IsSome)

[<Test>]
let ``Utilities\downcastAndCreateOption returns None with unrelated type`` () =
    let v = Test3()
    let test = downcastAndCreateOption<Test1>(v)
    Assert.AreEqual(true, test.IsNone)

[<Test>]
let ``Utilities\downcastAndCreateOption returns None with null`` () =
    let v : Test1 = null
    let test = downcastAndCreateOption<Test1>(v)
    Assert.AreEqual(true, test.IsNone)

[<Test>]
let ``Utilities\getPropertyNameFromExpression returns proper name`` () =
    let v = Test1()
    let name = getPropertyNameFromExpression <@ v.Test @>
    Assert.AreEqual("Test", name)

[<Test>]
let ``Utilities\getPropertyNameFromExpression returns empty string on non-property`` () =
    let v = Test1()
    let name = getPropertyNameFromExpression <@ v.TestMethod() @>
    Assert.AreEqual("", name)
