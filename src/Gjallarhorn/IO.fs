namespace Gjallarhorn.Interaction

open Gjallarhorn
open Gjallarhorn.Validation

open System

/// A single step in a validation chain
type Validation<'a,'b> = (Validation.ValidationCollector<'a> -> Validation.ValidationCollector<'b>)

/// Used to directly map a signal to a user. No notification exists in this case.
type Direct<'a> (input : ISignal<'a>) =
    /// Gets the signal
    member __.GetValue () = input

/// Used to report data to a user
type Report<'a,'b when 'a : equality and 'b : equality> (input : ISignal<'a>, conversion : 'a -> 'b) =
    let source = Signal.map conversion input

    /// Signal used as a notification mechanism. 
    member __.UpdateStream  = source

    /// Gets the current value
    member __.GetValue () = source.Value

/// Used to report data to a user with validation
type ValidatedReport<'a, 'b when 'a : equality and 'b : equality> private (input : ISignal<'a>, conversion : 'a -> 'b) =
    inherit Report<'a, 'b>(input, conversion)
    
    /// The validation results as a signal
    member val private ValidationInternal = (ValueNone:IValidatedSignal<'b,'b> voption) with get, set
    member this.Validation = this.ValidationInternal
    new(input : ISignal<'a>, conversion : 'a -> 'b, validation : Validation<'b, 'b>) as this =
        ValidatedReport<'a,'b>(input, conversion)
        then
            this.ValidationInternal <-
                this.UpdateStream
                |> Signal.validate validation
                |> ValueSome

/// Used as an input and output mapping to report and fetch data from a user
type InOut<'a, 'b when 'a : equality and 'b : equality> (input : ISignal<'a>, conversion : 'a -> 'b) =    
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
type ValidatedInOut<'a, 'b, 'c when 'a : equality and 'b : equality> private (input : ISignal<'a>, conversion : 'a -> 'b) =
    inherit InOut<'a, 'b>(input, conversion)

    member val private Validation = ValueNone: IValidatedSignal<'b,'c> voption with get, set

    /// The validated output data from the user interaction
    member this.Output = this.Validation
    new(input : ISignal<'a>, conversion : 'a -> 'b, validation : Validation<'b, 'c>) as this =
        new ValidatedInOut<'a,'b,'c>(input, conversion)
        then
            this.Validation <-
                this.UpdateStream
                |> Signal.validate validation
                |> ValueSome

/// Used as an output mapping to fetch data from a user
type Out<'a when 'a : equality> (initialValue : 'a) =    
    let editSource = Mutable.create initialValue
    
    /// Signal used as a notification mechanism for reporting
    member __.UpdateStream = editSource :> ISignal<_>

    /// Gets the current value
    member __.GetValue () = editSource.Value

    /// Updates the value to the output stream
    member __.SetValue v = editSource.Value <- v

/// Used as an output mapping with validation to fetch data from a user
type ValidatedOut<'a, 'b when 'a : equality> private (initialValue : 'a) =
    inherit Out<'a>(initialValue)    

    member val private Validation = ValueNone:IValidatedSignal<'a,'b> voption with get, set

    /// The validated output data from the user interaction
    member this.Output = this.Validation

    new(initialValue : 'a, validation : Validation<'a, 'b>) as this =
        ValidatedOut<'a,'b>(initialValue)
        then
            this.Validation <-
                this.UpdateStream
                |> Signal.validate validation
                |> ValueSome
    
/// Used as an input and output mapping which mutates an input IMutatable, with validation to report and fetch data from a user
type MutatableInOut<'a,'b when 'a : equality and 'b : equality> private (input : IMutatable<'a>, conversion : 'a -> 'b, validation : Validation<'b, 'a>, dummy:unit) =
    inherit ValidatedInOut<'a,'b, 'a>(input, conversion, validation)

    member val private Subscription:IDisposable voption = ValueNone with get, set

    interface IDisposable with
        member this.Dispose() =
            this.Subscription |> ValueOption.iter (fun disp -> disp.Dispose())
    new(input : IMutatable<'a>, conversion : 'a -> 'b, validation : Validation<'b, 'a>) as this =
        new MutatableInOut<'a,'b>(input, conversion, validation, ())
        then
            this.Subscription <-
                this.Output |> ValueOption.map (fun output ->
                    this.UpdateStream
                    |> Signal.Subscription.create(fun _ ->
                        if output.IsValid then
                            input.Value <- Option.get output.Value)
                )

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
        let create<'a when 'a : equality> signal =
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
        let validated<'a when 'a : equality> validation mutatable = 
            new MutatableInOut<'a,'a>(mutatable, id, validation)

        /// Create a simple input handle which pipes from the signal to conversion, user, validates to output, writes back to mutable
        let convertedValidated conversion validation mutatable =
            new MutatableInOut<_,_>(mutatable, conversion, validation)
