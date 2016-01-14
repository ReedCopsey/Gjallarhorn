(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/Gjallarhorn"
#I "../../bin/Gjallarhorn.Bindable"
#I "../../bin/Gjallarhorn.Bindable.Wpf"
#r "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.1/Facades/System.Runtime.dll"

(**
Validation in Gjallarhorn
========================

Mutables in Gjallarhorn also provide a mechanism to filter based on validation rules.

The validation engine in Gjallarhorn can be used to validate any value.  It is based on using function composition
with built-in functions defining rules.

For example, we can validate a string:

*)

#r "Gjallarhorn.dll"
open Gjallarhorn
open Gjallarhorn.Validation

let input = "Foo"

// Define a validation using function composition
let isValid v = 
    validate v 
    |> (notNullOrWhitespace >> noSpaces >> hasLengthAtLeast 3) 
    |> result

let testInput = isValid input

// Prints: "Foo" validation result: Valid
printfn "%A validation result: %A" input testInput

let badinput = " a"
let testBad = isValid badinput

// Prints: "Ba" validation result: Invalid ["Value cannot contain a space."; "Value must be at least 3 characters long."]
printfn "%A validation result: %A" badinput testBad

(**

Note that our validation result, with `"Foo"` as input, was Valid - all of our rules were met.

However, when passing `" a"`, we violated two of our input rules, and the resulting `ValidationResult` contains a list of all bad messages.

If you want to restrict the number of messages, you can use `fixErrors` (to stop collecting errors if we're already in an error state), or
`fixErrorsWithMessage` to stop collecting further errors as well as provide a custom error message.

This can be useful, for example, if validating a name as input.  When the user hasn't entered anything (it's null or whitespace), we likely
don't want to continue collecting errors, and instead want to report a custom error message:

*)

let validateName = notNullOrWhitespace >> fixErrorsWithMessage "Please enter your name" >> hasLengthAtLeast 2

let isValidName n = 
    validate n 
    |> validateName
    |> Validation.result

// Prints: Reed is valid?: Valid
printfn "Reed is valid: %A" (isValidName "Reed")

// Prints: An empty string is valid?: Invalid ["Please enter your name"]
printfn "An empty string is valid?: %A" (isValidName "")

// Prints: R is valid: Invalid ["Value must be at least 2 characters long."]
printfn "R is valid: %A" (isValidName "R")

(**

Again, using `fixErrors` or `fixErrorsWithMessage` stops collecting more errors, as demonstrated above.  Even though an empty
string would fail the `hasLengthAtLeast` rule, that error is not added to the list of invalid reasons.

We can also do custom validation rules via the `custom` function.  This accepts a function which takes the value, and returns a `string option`. When `None`,
the rule succeeds, when `Some`, the error is added to the invalid message list:

*)

let customRule name =
    match name with
    | "Reed" -> Some "Reed is a poor choice of names!"
    | _ -> None

let customValidation n = 
    validate n 
    |> (notNullOrWhitespace >> custom customRule)
    |> Validation.result

// Prints: Reed is valid: Invalid ["Reed is a poor choice of names!"]
printfn "Reed is valid: %A" (customValidation "Reed")

// Prints: James is valid: Valid
printfn "James is valid: %A" (customValidation "James")

(**

Now that we've seen how to do basic validation, we'll extend this into [Validating Views and Mutables](validate_types.html)

*)

