module Gjallarhorn.Tests.Validate

open Gjallarhorn

open Gjallarhorn.Validation

open NUnit.Framework

[<Test>]
let ``Signal\validate works on value`` () =
    let value = Mutable.create 12
    
    let validate = Signal.validate (greaterThan 24 >> lessThan 64) value
        
    validate.IsValid |> Assert.IsFalse

    value.Value <- 42
    validate.IsValid |> Assert.IsTrue

    value.Value <- 2193
    validate.IsValid |> Assert.IsFalse

[<Test>]
let ``Signal\validate works on string`` () =
    let value = Mutable.create ""
    
    let validate = Signal.validate (notNullOrWhitespace >> noSpaces >> notEqual "Reed") value
        
    validate.IsValid |> Assert.IsFalse

    value.Value <- "Re"
    validate.IsValid |> Assert.IsTrue

    value.Value <- "Reed"
    validate.IsValid |> Assert.IsFalse

    value.Value <- "Foo Bar"
    validate.IsValid |> Assert.IsFalse

    value.Value <- null
    validate.IsValid |> Assert.IsFalse

    value.Value <- "Good"
    validate.IsValid |> Assert.IsTrue

[<Test>]
let ``Signal\validate provides proper error messages`` () =
    let value = Mutable.create ""
    
    let validate = Signal.validate (notNullOrWhitespace >> hasLengthAtLeast 2 >> noSpaces) value
        
    match validate.ValidationResult.Value with
    | Valid -> Assert.Fail()
    | Invalid(errors) ->
        Assert.AreEqual(2, errors.Length)
        Assert.Contains(box "Value cannot be null or empty.", Seq.toArray(errors))

    value.Value <- "Re"
    match validate.ValidationResult.Value with
    | Valid -> ()
    | Invalid(errors) -> Assert.Fail()

    value.Value <- " "
    match validate.ValidationResult.Value with
    | Valid -> Assert.Fail()
    | Invalid(errors) ->
        Assert.AreEqual(3, errors.Length)
        Assert.Contains(box "Value must be at least 2 characters long.", Seq.toArray(errors))
        Assert.Contains(box "Value cannot contain a space.", Seq.toArray(errors))

[<Test>]
let ``Signal\validate signals properly when value changes`` () =
    let value = Mutable.create ""
    let validated = Signal.validate notNullOrWhitespace value
    
    let states = ResizeArray<ValidationResult>()

    Signal.Subscription.create states.Add validated.ValidationResult 
    |> ignore

    Assert.AreEqual(0, states.Count)
    value.Value <- ""
    Assert.AreEqual(0, states.Count)
    value.Value <- "Test"
    Assert.AreEqual(1, states.Count)
    Assert.IsTrue(ValidationResult.Valid = states.[0])
    value.Value <- "Change to another valid state"
    Assert.AreEqual(1, states.Count)
    Assert.IsTrue(ValidationResult.Valid = states.[0])
    value.Value <- ""
    Assert.AreEqual(2, states.Count)

    match states.[1] with
    | Valid -> Assert.Fail()
    | Invalid(errors) ->
        Assert.AreEqual(1, errors.Length)
        Assert.Contains(box "Value cannot be null or empty.", Seq.toArray(errors))

    match validated.ValidationResult.Value with
    | Valid -> Assert.Fail()
    | Invalid(errors) ->
        Assert.AreEqual(1, errors.Length)
        Assert.Contains(box "Value cannot be null or empty.", Seq.toArray(errors))

[<Test>]
let ``Signal\validate subscriptions last through GC collections`` () =
    let value = Mutable.create ""
    let validated = Signal.validate notNullOrWhitespace value
    
    let states = ResizeArray<ValidationResult>()

    use subscription = Signal.Subscription.create states.Add validated.ValidationResult 
    // |> ignore

    System.GC.Collect();
    Assert.AreEqual(0, states.Count)
    value.Value <- ""
    System.GC.Collect();
    Assert.AreEqual(0, states.Count)
    value.Value <- "Test"
    System.GC.Collect();
    Assert.AreEqual(1, states.Count)
    Assert.IsTrue(ValidationResult.Valid = states.[0])
    value.Value <- "Change to another valid state"
    System.GC.Collect();
    Assert.AreEqual(1, states.Count)
    Assert.IsTrue(ValidationResult.Valid = states.[0])
    value.Value <- ""
    System.GC.Collect();
    Assert.AreEqual(2, states.Count)

    match states.[1] with
    | Valid -> Assert.Fail()
    | Invalid(errors) ->
        Assert.AreEqual(1, errors.Length)
        Assert.Contains(box "Value cannot be null or empty.", Seq.toArray(errors))

    match validated.ValidationResult.Value with
    | Valid -> Assert.Fail()
    | Invalid(errors) ->
        Assert.AreEqual(1, errors.Length)
        Assert.Contains(box "Value cannot be null or empty.", Seq.toArray(errors))

    // Keep this alive for release testing
    Assert.IsNotNull(subscription)
[<Test>]
let ``Signal\validate provides proper error messages when fixed`` () =
    let value = Mutable.create ""
    let validate = Signal.validate (notNullOrWhitespace >> hasLengthAtLeast 2 >> noSpaces) value
        
    match validate.ValidationResult.Value with
    | Valid -> Assert.Fail()
    | Invalid(errors) ->
        Assert.AreEqual(2, errors.Length)
        Assert.Contains(box "Value cannot be null or empty.", Seq.toArray(errors))

    value.Value <- "Re"
    match validate.ValidationResult.Value with
    | Valid -> ()
    | Invalid(errors) -> Assert.Fail()

    value.Value <- " "
    match validate.ValidationResult.Value with
    | Valid -> Assert.Fail()
    | Invalid(errors) ->
        Assert.AreEqual(3, errors.Length)
        Assert.Contains(box "Value must be at least 2 characters long.", Seq.toArray(errors))
        Assert.Contains(box "Value cannot contain a space.", Seq.toArray(errors))
