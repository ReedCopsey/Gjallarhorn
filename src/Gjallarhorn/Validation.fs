namespace Gjallarhorn

open System
open System.Text.RegularExpressions

/// Basic validation support
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
        | ValidationCollector.Invalid(value, errors, InvalidValidationStatus.CollectingMessages) -> 
            // Switch us from invalid to invalid with errors fixed
            ValidationCollector.Invalid(value, [errorMessage], InvalidValidationStatus.Completed)
        | _ -> step

    /// Create a custom validator using a predicate ('a -> bool) and an error message on failure. The error message can use {0} for a placeholder for the property name.
    let custom (validator : 'a -> string option) (step : ValidationCollector<'a>) =        
        let success = 
            match step with            
            | ValidationCollector.Invalid(_, _, InvalidValidationStatus.Completed) -> None // Short circuit
            | ValidationCollector.Invalid(value, _, _) -> validator value
            | ValidationCollector.Valid(value) -> validator value        
        match success, step with
        | _, ValidationCollector.Invalid(_, _, InvalidValidationStatus.Completed) -> step // If our errors are fixed coming in, just pass through
        | None, ValidationCollector.Valid(value) -> ValidationCollector.Valid(value)
        | None,  ValidationCollector.Invalid(value, err, status) -> ValidationCollector.Invalid(value, err, status)
        | Some error, ValidationCollector.Valid(value) -> ValidationCollector.Invalid(value, [String.Format(error, value)], InvalidValidationStatus.CollectingMessages)
        | Some error, ValidationCollector.Invalid(value, err, status) -> ValidationCollector.Invalid(value, err @ [String.Format(error, value)], status)


    [<AutoOpen>]
    module Validators =    
        // String validations
        let notNullOrWhitespace (str : ValidationCollector<string>) = 
            let validation value = if String.IsNullOrWhiteSpace(value) then Some "Value cannot be null or empty." else None            
            custom validation  str 

        let noSpaces (str : ValidationCollector<string>) = 
            let validation (value : string) = if not(String.IsNullOrEmpty(value)) && value.Contains(" ") then Some "Value cannot contain a space." else None
            custom validation str

        let hasLength (length : int) (str : ValidationCollector<string>) = 
            let validation (value : string) = if (value = null && length <> 0 ) || value.Length <> length then Some ("Value must be " + length.ToString() + " characters long.") else None
            custom validation str

        let hasLengthAtLeast (length : int) (str : ValidationCollector<string>) = 
            let validation (value : string) = if (value = null && length <> 0 ) || value.Length < length then Some ("Value must be at least " + length.ToString() + " characters long.") else None
            custom validation str

        let hasLengthNoLongerThan (length : int) (str : ValidationCollector<string>) = 
            let validation (value : string) = if not(String.IsNullOrWhiteSpace(value)) && value.Length > length then Some ("Value must be no longer than " + length.ToString() + " characters long") else None
            custom validation str
        
        let private matchesPatternInternal (pattern : string) (errorMsg : string) (str : ValidationCollector<string>) =
            let validation (value : string) = if not(String.IsNullOrWhiteSpace(value)) && Regex.IsMatch(value, pattern) then None else Some errorMsg
            custom validation str

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
            custom validation step

        let greaterThan value step =
            let validation v = if v > value then None else Some ("Value must be greater than " + value.ToString())
            custom validation step

        let greaterOrEqualTo value step =
            let validation v = if v >= value then None else Some ("Value must be greater than or equal to " + value.ToString())
            custom validation step

        let lessThan value step =
            let validation v = if v < value then None else Some ("Value must be less than " + value.ToString())
            custom validation step

        let lessOrEqualTo value step =
            let validation v = if v <= value then None else Some ("Value must be less than or equal to " + value.ToString())
            custom validation step

        let isBetween lowerBound upperBound step =
            let validation v = if lowerBound <= v && v <= upperBound then None else Some ("Value must be between " + lowerBound.ToString() + " and " + upperBound.ToString())
            custom validation step
    
        let containedWithin collection step =
            let validation value = if Option.isSome (Seq.tryFind ((=) value) collection) then None else Some ("{0} must be one of: " + String.Join(", ", Seq.map (fun i -> i.ToString()) collection))
            custom validation step

        let notContainedWithin collection step =
            let validation value = if Option.isNone (Seq.tryFind ((=) value) collection) then None else Some ("{0} cannot be one of: " + String.Join(", ", Seq.map (fun i -> i.ToString()) collection))
            custom validation step

    /// Defines a validation result
    type ValidationResult =
    /// Value is valid
    | Valid
    /// Value is invalid with a list of error messages
    | Invalid of errors : string list

    /// Check to see if a result is in a valid state
    let isValid result =
        match result with 
        | Valid -> true
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
        | ValidationCollector.Invalid(_, _, _) -> ValidationResult.Invalid([customErrorMessage])


    /// Core interface for all validated signal types
    type IValidatedSignal<'a> =
        inherit ISignal<'a>
    
        /// The current validation status
        abstract member ValidationResult : ISignal<ValidationResult> with get

        /// Check to see if type is currently in a valid state
        abstract member IsValid : bool with get

