namespace Gjallarhorn

open System
open System.Text.RegularExpressions
open System.Runtime.InteropServices

/// Defines a validation result
type ValidationResult =
/// Value is valid
| Valid
/// Value is invalid with a list of error messages
| Invalid of errors : string list
with
    static member private ValidResultAsList = [ "" ]

    /// Check to see if we're in a valid state
    member this.IsValidResult =
        match this with
        | Valid -> true
        | _ -> false

    /// Convert to a list of strings. If forceOutput is true, the list will have a single, empty
    /// string in valid cases 
    member this.ToList (forceOutput : bool) =
        match this, forceOutput with
        | Valid, true -> ValidationResult.ValidResultAsList
        | Valid, false -> ValidationResult.ValidResultAsList
        | Invalid(errors), _ -> errors
        |> ResizeArray<_>

/// The output of validating an input signal
type IValidatedSignal<'a, 'b> =
    inherit ISignal<'b option>
                
    /// The raw, unvalidated input
    abstract member RawInput : ISignal<'a> with get

    /// The current validation status
    abstract member ValidationResult : ISignal<ValidationResult> with get

    /// Check to see if type is currently in a valid state
    abstract member IsValid : bool with get        

/// Basic validation support
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Validation =

    /// Used to track the status of invalid validations
    type InvalidValidationStatus =
    /// More messages are being collected
    | CollectingMessages
    /// Message collecting is complete
    | Completed

    /// Defines the Validation as being in one of three possible states
    type ValidationCollector<'a> =
    /// Validation in a valid state 
    | Valid of value : 'a
    /// Validation in an invalid state
    | Invalid of value : 'a * error : string list * status : InvalidValidationStatus

    /// Begin a validation chain for a given property
    let validate value = ValidationCollector.Valid(value)

    /// Fix the current state of errors, bypassing all future validation checks if we're in an error state
    let fixErrors (step : ValidationCollector<'a>) =
        match step with
        | ValidationCollector.Invalid(value, errors, InvalidValidationStatus.CollectingMessages) -> 
            // Switch us from invalid to invalid with errors fixed
            ValidationCollector.Invalid(value, errors, InvalidValidationStatus.Completed)
        | _ -> step

    /// Fix the current state of errors, bypassing all future validation checks if we're in an error state
    /// Also supplies a custom error message to replace the existing
    let fixErrorsWithMessage errorMessage (step : ValidationCollector<'a>) =
        match step with
        | ValidationCollector.Invalid(value, _, InvalidValidationStatus.CollectingMessages) -> 
            // Switch us from invalid to invalid with errors fixed
            ValidationCollector.Invalid(value, [errorMessage], InvalidValidationStatus.Completed)
        | _ -> step

    /// Create a custom validator using a custom function ('a -> string option) . The error message can use {0} for a placeholder for the property value. None indicates success.
    let validateWith (validator : 'a -> string option) (step : ValidationCollector<'a>) =        
        let success = 
            match step with            
            | ValidationCollector.Invalid(_, _, InvalidValidationStatus.Completed) -> None // Short circuit
            | ValidationCollector.Invalid(value, _, _) -> validator value
            | ValidationCollector.Valid(value) -> validator value        
        match success, step with
        | _, ValidationCollector.Invalid(_, _, InvalidValidationStatus.Completed) -> step // If our errors are fixed coming in, just pass through
        | None, ValidationCollector.Valid(value) -> ValidationCollector.Valid(value)
        | None,  ValidationCollector.Invalid(value, err, status) -> ValidationCollector.Invalid(value, err, status)
        | Some error, ValidationCollector.Valid(value) -> ValidationCollector.Invalid(value, [error], InvalidValidationStatus.CollectingMessages)
        | Some error, ValidationCollector.Invalid(value, err, status) -> ValidationCollector.Invalid(value, err @ [error], status)

    /// Create a custom converter validator using a function ('a -> Choice<'b,string>) and default value on conversion failure. Choice2of2 indicates a failure error message. The error message can use {0} for a placeholder for the property name.  Conversions always stop collecting on failure.
    let customConverter (validator : 'a -> Choice<'b,string>) (defaultOnFailure: 'b) (step : ValidationCollector<'a>) =        
        match step with            
        | ValidationCollector.Invalid(value, err, InvalidValidationStatus.Completed) -> 
            match validator value with
            | Choice1Of2 newValue -> ValidationCollector.Invalid(newValue, err, InvalidValidationStatus.Completed)
            | Choice2Of2 _ -> ValidationCollector.Invalid(defaultOnFailure, err, InvalidValidationStatus.Completed)
        | ValidationCollector.Invalid(value, err, InvalidValidationStatus.CollectingMessages) -> 
            match validator value with
            | Choice1Of2 newValue -> ValidationCollector.Invalid(newValue, err, InvalidValidationStatus.Completed)
            | Choice2Of2 error -> ValidationCollector.Invalid(defaultOnFailure, err @ [ error ], InvalidValidationStatus.Completed)
        | ValidationCollector.Valid(value) -> 
            match validator value with
            | Choice1Of2 newValue -> ValidationCollector.Valid(newValue)
            | Choice2Of2 error -> ValidationCollector.Invalid(defaultOnFailure, [ error ], InvalidValidationStatus.Completed)

    /// Library of validation converters which can be used to convert value representations as part of the validation process
    module Converters =
        /// An "id" style conversion which does nothing
        let toSelf (input : ValidationCollector<'a>) = 
            let convert (value : 'a) = Choice1Of2 (value)
            let value =
                match input with
                | ValidationCollector.Valid v -> v
                | ValidationCollector.Invalid (v,_,_) -> v
            customConverter convert value input

        /// Convert to a string representation using Object.ToString()
        let toString (input : ValidationCollector<'a>) = 
            let convert (value : 'a) = Choice1Of2 (value.ToString())
            customConverter convert "" input

        /// Convert between any two types, using System.Convert.ChangeType
        let fromTo<'a,'b> (input : ValidationCollector<'a>) : ValidationCollector<'b> =
            let convert (value : 'a) = 
                try
                    Choice1Of2 <| (System.Convert.ChangeType(box value, typeof<'b>) :?> 'b)
                with
                | _ -> 
                    Choice2Of2 "Value could not be converted."

            customConverter convert Unchecked.defaultof<'b> input

        /// Convert from a string to an integer specifying culture information
        let stringToInt32C style provider input = 
            let convert (value : string) = 
                match System.Int32.TryParse(value,style,provider) with
                | false, _ -> Choice2Of2 "Value does not represent a valid number."
                | true, v -> Choice1Of2 v
            customConverter convert Unchecked.defaultof<int> input

        /// Convert from a string to an integer
        let stringToInt32 input = 
            let convert (value : string) = 
                match System.Int32.TryParse value with
                | false, _ -> Choice2Of2 "Value does not represent a valid number."
                | true, v -> Choice1Of2 v
            customConverter convert Unchecked.defaultof<int> input

        /// Convert from a string to a 64bit float
        let stringToDouble input = 
            let convert (value : string) = 
                match System.Double.TryParse value with
                | false, _ -> Choice2Of2 "Value does not represent a valid number."
                | true, v -> Choice1Of2 v
            customConverter convert Unchecked.defaultof<float> input

        /// Convert from a string to a double specifying culture information
        let stringToDoubleC style provider input = 
            let convert (value : string) = 
                match System.Double.TryParse(value,style,provider) with
                | false, _ -> Choice2Of2 "Value does not represent a valid number."
                | true, v -> Choice1Of2 v
            customConverter convert Unchecked.defaultof<float> input

    module Validators =    
        // Simple validator that does nothing
        let noValidation input =
            input

        // String validations
        let notNullOrWhitespace (str : ValidationCollector<string>) = 
            let validation value = if String.IsNullOrWhiteSpace(value) then Some "Value cannot be null or empty." else None            
            validateWith validation  str 

        let noSpaces (str : ValidationCollector<string>) = 
            let validation (value : string) = if not(String.IsNullOrEmpty(value)) && value.Contains(" ") then Some "Value cannot contain a space." else None
            validateWith validation str

        let hasLength (length : int) (str : ValidationCollector<string>) = 
            let validation (value : string) = if (value = null && length <> 0 ) || value.Length <> length then Some ("Value must be " + length.ToString() + " characters long.") else None
            validateWith validation str

        let hasLengthAtLeast (length : int) (str : ValidationCollector<string>) = 
            let validation (value : string) = if (value = null && length <> 0 ) || value.Length < length then Some ("Value must be at least " + length.ToString() + " characters long.") else None
            validateWith validation str

        let hasLengthNoLongerThan (length : int) (str : ValidationCollector<string>) = 
            let validation (value : string) = if not(String.IsNullOrWhiteSpace(value)) && value.Length > length then Some ("Value must be no longer than " + length.ToString() + " characters long") else None
            validateWith validation str
        
        let private matchesPatternInternal (pattern : string) (errorMsg : string) (str : ValidationCollector<string>) =
            let validation (value : string) = if not(String.IsNullOrWhiteSpace(value)) && Regex.IsMatch(value, pattern) then None else Some errorMsg
            validateWith validation str

        let matchesPattern (pattern : string) str =
            matchesPatternInternal pattern ("Value must match following pattern: " + pattern) str

        let isAlphanumeric str =
            matchesPatternInternal "[^a-zA-Z0-9]" "Value must be alphanumeric" str

        let containsAtLeastOneDigit str = 
            matchesPatternInternal "[0-9]" "Value must contain at least one digit" str

        let containsAtLeastOneUpperCaseCharacter str =
            matchesPatternInternal "[A-Z]" "Value must contain at least one uppercase character" str

        let containsAtLeastOneLowerCaseCharacter str =
            matchesPatternInternal "[a-z]" "Value must contain at least one lowercase character" str

        // Generic validations
        let notEqual value step = 
            let validation v = if value = v then Some ("Value cannot equal " + value.ToString()) else None
            validateWith validation step

        let greaterThan value step =
            let validation v = if v > value then None else Some ("Value must be greater than " + value.ToString())
            validateWith validation step

        let greaterOrEqualTo value step =
            let validation v = if v >= value then None else Some ("Value must be greater than or equal to " + value.ToString())
            validateWith validation step

        let lessThan value step =
            let validation v = if v < value then None else Some ("Value must be less than " + value.ToString())
            validateWith validation step

        let lessOrEqualTo value step =
            let validation v = if v <= value then None else Some ("Value must be less than or equal to " + value.ToString())
            validateWith validation step

        let isBetween lowerBound upperBound step =
            let validation v = if lowerBound <= v && v <= upperBound then None else Some ("Value must be between " + lowerBound.ToString() + " and " + upperBound.ToString())
            validateWith validation step
    
        let containedWithin collection step =
            let validation value = if Seq.contains value collection then None else Some ("Value must be one of: " + String.Join(", ", Seq.map (fun i -> i.ToString()) collection))
            validateWith validation step

        let notContainedWithin collection step =
            let validation value = if Seq.contains value collection then Some ("Value cannot be one of: " + String.Join(", ", Seq.map (fun i -> i.ToString()) collection)) else None
            validateWith validation step


    /// Check to see if a result is in a valid state
    let isValid result =
        match result with 
        | ValidationResult.Valid -> true
        | _ -> false

    /// Extracts the resulting errors from an invalid validation, or an empty list for success
    let result (step : ValidationCollector<'a>) : ValidationResult =
        match step with
        | ValidationCollector.Valid(_) -> ValidationResult.Valid
        | ValidationCollector.Invalid(_, err, _) -> ValidationResult.Invalid(err)

    /// Produces a result of the validation, using a custom error message if an error occurred
    let resultWithError customErrorMessage (step : ValidationCollector<'a>) : ValidationResult =
        match step with
        | ValidationCollector.Valid(_) -> ValidationResult.Valid
        | ValidationCollector.Invalid(_) -> ValidationResult.Invalid([customErrorMessage])


//    /// Core interface for all validated signal types
//    type IValidatedSignal<'a> =
//        inherit ISignal<'a>
//    
//        /// The current validation status
//        abstract member ValidationResult : ISignal<ValidationResult> with get
//
//        /// Check to see if type is currently in a valid state
//        abstract member IsValid : bool with get
//
