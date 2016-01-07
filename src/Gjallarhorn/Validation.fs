namespace Gjallarhorn

open Gjallarhorn.Internal

open System
open System.Text.RegularExpressions

/// Provides mechanisms for working with IView views
module Validation =

    /// Defines the Validation as being in one of three possible states
    type ValidationResult<'a> =
    /// Validation in a valid state - name and a value exist
    | Valid of name : string * value : 'a
    /// Validation in an invalid state, with more validations potentially being collected
    | InvalidCollecting of name : string * value : 'a * error : string list
    /// Validation in a completed state, with more validations being skipped
    | InvalidDone of name : string * value : 'a * error : string list

    /// Begin a validation chain for a given property name
    let validate propertyName value = Valid(propertyName, value)

    /// Fix the current state of errors, bypassing all future validation checks if we're in an error state
    let fixErrors (step : ValidationResult<'a>) =
        match step with
        | InvalidCollecting(name, value, errors) -> 
            // Switch us from invalid to invalid with errors fixed
            InvalidDone(name, value, errors)
        | _ -> step

    /// Fix the current state of errors, bypassing all future validation checks if we're in an error state
    /// Also supplies a custom error message to replace the existing
    let fixErrorsWithMessage errorMessage (step : ValidationResult<'a>) =
        match step with
        | InvalidCollecting(name, value, errors) -> 
            // Switch us from invalid to invalid with errors fixed
            InvalidDone(name, value, [errorMessage])
        | _ -> step

    /// Create a custom validator using a predicate ('a -> bool) and an error message on failure. The error message can use {0} for a placeholder for the property name.
    let custom (validator : 'a -> string option) (step : ValidationResult<'a>) =        
        let success = 
            match step with            
            | InvalidDone(_, _, _) -> None // Short circuit
            | InvalidCollecting(_, value, _) -> validator value
            | Valid(_, value) -> validator value        
        match success, step with
        | _, InvalidDone(_, _, _) -> step // If our errors are fixed coming in, just pass through
        | None, Valid(name, value) -> Valid(name, value)
        | None,  InvalidCollecting(name, value, err) -> InvalidCollecting(name, value, err)
        | Some error, Valid(name, value) -> InvalidCollecting(name, value, [String.Format(error, name, value)])
        | Some error, InvalidCollecting(name, value, err) -> InvalidCollecting(name, value, err @ [String.Format(error, name, value)])

    let result (step : ValidationResult<'a>) : string list =
        match step with
        | Valid(_, _) -> []
        | InvalidCollecting(_, _, err) -> err
        | InvalidDone(_, _, err) -> err

    /// Produces a result of the validation, using a custom error message if an error occurred
    let resultWithError customErrorMessage (step : ValidationResult<'a>) : string list =
        match step with
        | Valid(_, value) -> []
        | _ -> [customErrorMessage]

    [<AutoOpen>]
    module Validators =    
        // String validations
        let notNullOrWhitespace (str : ValidationResult<string>) = 
            let validation value = if String.IsNullOrWhiteSpace(value) then Some "{0} cannot be null or empty." else None            
            custom validation  str 

        let noSpaces (str : ValidationResult<string>) = 
            let validation (value : string) = if value.Contains(" ") then Some "{0} cannot contain a space." else None
            custom validation str

        let hasLength (length : int) (str : ValidationResult<string>) = 
            let validation (value : string) = if value.Length <> length then Some ("{0} must be " + length.ToString() + " characters long.") else None
            custom validation str

        let hasLengthAtLeast (length : int) (str : ValidationResult<string>) = 
            let validation (value : string) = if value.Length < length then Some ("{0} must be at least " + length.ToString() + " characters long.") else None
            custom validation str

        let hasLengthNoLongerThan (length : int) (str : ValidationResult<string>) = 
            let validation (value : string) = if value.Length > length then Some ("{0} must be no longer than " + length.ToString() + " characters long") else None
            custom validation str
        
        let private matchesPatternInternal (pattern : string) (errorMsg : string) (str : ValidationResult<string>) =
            let validation (value : string) = if Regex.IsMatch(value, pattern) then None else Some errorMsg
            custom validation str

        let matchesPattern (pattern : string) str =
            matchesPatternInternal pattern ("{0} must match following pattern: " + pattern) str

        let isAlphanumeric str =
            matchesPatternInternal "[^a-zA-Z0-9]" "{0} must be alphanumeric" str

        let containsAtLeastOneDigit str = 
            matchesPatternInternal "[0-9]" "{0} must contain at least one digit" str

        let containsAtLeastOneUpperCaseCharacter str =
            matchesPatternInternal "[A-Z]" "{0} must contain at least one uppercase character" str

        let containsAtLeastOneLowerCaseCharacter str =
            matchesPatternInternal "[a-z]" "{0} must contain at least one lowercase character" str

        // Generic validations
        let notEqual value step = 
            let validation v = if value = v then Some ("{0} cannot equal " + value.ToString()) else None
            custom validation step

        let greaterThan value step =
            let validation v = if v > value v then None else Some ("{0} must be greater than " + value.ToString())
            custom validation step

        let greaterOrEqualTo value step =
            let validation v = if v >= value v then None else Some ("{0} must be greater than or equal to " + value.ToString())
            custom validation step

        let lessThan value step =
            let validation v = if v < value v then None else Some ("{0} must be less than " + value.ToString())
            custom validation step

        let lessOrEqualTo value step =
            let validation v = if v <= value v then None else Some ("{0} must be less than or equal to " + value.ToString())
            custom validation step

        let isBetween lowerBound upperBound step =
            let validation v = if lowerBound <= v && v <= upperBound then None else Some ("{0} must be between " + lowerBound.ToString() + " and " + upperBound.ToString())
            custom validation step
    
        let containedWithin collection step =
            let validation value = if Option.isSome (Seq.tryFind ((=) value) collection) then None else Some ("{0} must be one of: " + String.Join(", ", Seq.map (fun i -> i.ToString()) collection))
            custom validation step

        let notContainedWithin collection step =
            let validation value = if Option.isNone (Seq.tryFind ((=) value) collection) then None else Some ("{0} cannot be one of: " + String.Join(", ", Seq.map (fun i -> i.ToString()) collection))
            custom validation step
