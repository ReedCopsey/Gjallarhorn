(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/Gjallarhorn"
#I "../../bin/Gjallarhorn.Bindable"
#I "../../bin/Gjallarhorn.Bindable.Wpf"
#r "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.1/Facades/System.Runtime.dll"

(**
Validating Views and Mutables
========================

Views and Mutables in Gjallarhorn also provide a mechanism to filter based on validation rules.

A review of the [basics of validation in Gjallarhorn](validation.html) prior to reading this section will be beneficial.

The validation engine in Gjallarhorn can be used to validate Views and Mutables directly.

`View.validate` allows you to filter a view into an `IValidatedView<'a>`, which provides two new properties:
- `ValidationResult`: a `IView<ValidationResult>` with details of the validation of the current value of the view
- `IsValid`: a boolean of whether the current view's value is valid

*)

#r "Gjallarhorn.dll"
open Gjallarhorn
open Gjallarhorn.Validation

let source = Mutable.create 0

// Create a view on the mutable, validating it with 3 rules on the fly:
let test = View.validate (notEqual 3 >> greaterThan 1 >> lessThan 5) source

// Prints: Valid?: false
printfn "Valid?: %b" test.IsValid

source.Value <- 2
// Prints: Valid?: true
printfn "Valid?: %b" test.IsValid

source.Value <- 3
// Prints: Validation: Invalid ["Value cannot equal 3"]
printfn "Validation: %A" test.ValidationResult.Value


(**

We can validate Mutables directly in a similar manner.  With a validated Mutable, represented
as the `IValidatedMutatable<'a>` type, any changes set into the mutable will automatically
be filtered, and not propogated down to the source unless the data is valid:

*)

let source = Mutable.create 0

// This time, create a mutable with validation
let editor = Mutable.validate (notEqual 3 >> greaterThan 1 >> lessThan 5) source

// Prints: Valid?: false
printfn "Valid?: %b" editor.IsValid

editor.Value <- 2
// Prints: Valid?: true
printfn "Valid?: %b" editor.IsValid

editor.Value <- 3
// Prints: Validation: Invalid ["Value cannot equal 3"]
printfn "Validation: %A" editor.ValidationResult.Value

// Note that we're currently not valid, but we set the editor.  Source will not have this change:
// Prints: Source = 2, Editor = 3
printfn "Source = %d, Editor = %d" source.Value editor.Value

editor.Value <- 4
// Prints: Source = 4, Editor = 4 [true]
printfn "Source = %d, Editor = %d [%b]" source.Value editor.Value editor.IsValid


