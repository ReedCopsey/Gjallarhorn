namespace Gjallarhorn.Interaction

open Gjallarhorn
open Gjallarhorn.Helpers
open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System
open System.Collections.Generic

/// A single step in a validation chain
type Validation<'a,'b> = (Validation.ValidationCollector<'a> -> Validation.ValidationCollector<'b>)

/// Used to report data to a user
type Report<'a,'b> (input : ISignal<'a>, conversion : 'a -> 'b) =
    let source = Signal.map conversion input

    /// Signal used as a notification mechanism. 
    member __.UpdateStream  = source
    /// Gets the current value
    member __.GetValue () = source.Value

/// Used to report data to a user with validation
type ValidatedReport<'a, 'b> (input : ISignal<'a>, conversion : 'a -> 'b, validation : Validation<'b, 'b>) as self =
    inherit Report<'a, 'b>(input, conversion)

    let validation = 
        self.UpdateStream
        |> Signal.validate validation
    
    /// The validation results as a signal
    member __.Validation = validation

/// Used as an input and output mapping to report and fetch data from a user
type InOut<'a, 'b> (input : ISignal<'a>, conversion : 'a -> 'b) =    
    let converted = Signal.map conversion input
    
    let editSource = Mutable.create converted.Value
    
    let subscription =  Signal.Subscription.copyTo editSource converted

    /// Signal used as a notification mechanism for reporting
    member __.UpdateStream = editSource :> ISignal<_>
    /// Gets the current value
    member __.GetValue () = editSource.Value
    /// Updates the value to the output stream
    member __.SetValue v = editSource.Value <- v
    
    interface IDisposable with
        member __.Dispose() =
            subscription.Dispose()

/// Used as an input and output mapping with validation to report and fetch data from a user
type ValidatedInOut<'a, 'b, 'c> (input : ISignal<'a>, conversion : 'a -> 'b, validation : Validation<'b, 'c>) as self =
    inherit InOut<'a, 'b>(input, conversion)

    let validation = 
        self.UpdateStream
        |> Signal.validate validation

    /// The validated output data from the user interaction
    member this.Output = validation

/// Used as an output mapping to fetch data from a user
type Out<'a> (initialValue : 'a) =    
    let editSource = Mutable.create initialValue
    
    /// Signal used as a notification mechanism for reporting
    member __.UpdateStream = editSource :> ISignal<_>
    /// Gets the current value
    member __.GetValue () = editSource.Value
    /// Updates the value to the output stream
    member __.SetValue v = editSource.Value <- v

/// Used as an output mapping with validation to fetch data from a user
type ValidatedOut<'a, 'b> (initialValue : 'a, validation : Validation<'a, 'b>) as self =
    inherit Out<'a>(initialValue)    

    let validation = 
        self.UpdateStream
        |> Signal.validate validation

    /// The validated output data from the user interaction
    member this.Output = validation
    
    
/// Used as an input and output mapping which mutates an input IMutatable, with validation to report and fetch data from a user
type MutatableInOut<'a,'b> (input : IMutatable<'a>, conversion : 'a -> 'b, validation : Validation<'b, 'a>) as self =
    inherit InOut<'a,'b>(input, conversion)

    let validated = 
        self.UpdateStream
        |> Signal.validate validation

    let subscription = 
        validated.ValidationResult
        |> Signal.Subscription.create(fun v ->
            if v.IsValidResult then
                input.Value <- Option.get validated.Value)

    interface IDisposable with
        member __.Dispose() =
            subscription.Dispose()

/// Creates IO handles for use with Gjallarhorn adapters, like Gjallarhorn.Bindable
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IO =
    /// Creates reporting handles
    module Report =
        /// Create a simple report which updates when the signal updates
        let create signal =
            Report(signal, id)
        /// Create a report which updates when the signal updates and uses a mapping function
        let converted conversion signal =
            Report(signal, conversion)
        /// Create a report which validates and updates when the signal updates
        let validated validation signal=
            ValidatedReport(signal, id, validation)
        /// Create a report which validates and updates when the signal updates and uses a mapping function
        let convertedValidated conversion validation signal =
            ValidatedReport(signal, conversion, validation)
    /// Creates input/output handles
    module InOut =
        /// Create a simple input handle which pipes from the signal to user, to output
        let create<'a> signal =
            new InOut<'a,'a>(signal, id)
        /// Create a simple input handle which pipes from the signal to conversion, to user, to output
        let converted conversion signal =
            new InOut<'a,'b>(signal, conversion)
        /// Create a simple input handle which pipes from the signal to user, validates to output
        let validated validation signal =
            new ValidatedInOut<_,_,_>(signal, id, validation)
        /// Create a simple input handle which pipes from the signal to conversion, to user, validates to output
        let convertedValidated conversion validation signal =
            new ValidatedInOut<_,_,_>(signal, conversion, validation)
    /// Creates input/output handles that directly mutate an input IMutatable
    module MutableInOut =
        /// Create a simple input handle which pipes from the signal to user, validates to output, writes back to mutable
        let validated<'a> validation mutatable = 
            new MutatableInOut<'a,'a>(mutatable, id, validation)
        /// Create a simple input handle which pipes from the signal to conversion, user, validates to output, writes back to mutable
        let convertedValidated conversion validation mutatable =
            new MutatableInOut<_,_>(mutatable, conversion, validation)
