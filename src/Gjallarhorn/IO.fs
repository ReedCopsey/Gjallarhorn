namespace Gjallarhorn.Interaction

open Gjallarhorn
open Gjallarhorn.Helpers
open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System
open System.Collections.Generic

type Validation<'a,'b> = (Validation.ValidationCollector<'a> -> Validation.ValidationCollector<'b>)

type Input<'a,'b> (input : ISignal<'a>, conversion : 'a -> 'b) =
    let source = Signal.map conversion input

    member __.UpdateStream  = source
    member __.GetValue () = source.Value

type ValidatedInput<'a, 'b> (input : ISignal<'a>, conversion : 'a -> 'b, validation : Validation<'b, 'b>) as self =
    inherit Input<'a, 'b>(input, conversion)

    let validation = 
        self.UpdateStream
        |> Signal.validate validation
    
    member __.Validation = validation

type InOut<'a, 'b> (input : ISignal<'a>, conversion : 'a -> 'b) =    
    let converted = Signal.map conversion input
    
    let editSource = Mutable.create converted.Value
    
    let subscription =  Signal.Subscription.copyTo editSource converted

    member __.UpdateStream = editSource :> ISignal<_>
    member __.GetValue () = editSource.Value
    member __.SetValue v = editSource.Value <- v
    
    interface IDisposable with
        member __.Dispose() =
            subscription.Dispose()

type ValidatedInOut<'a, 'b, 'c> (input : ISignal<'a>, conversion : 'a -> 'b, validation : Validation<'b, 'c>) as self =
    inherit InOut<'a, 'b>(input, conversion)

    let validation = 
        self.UpdateStream
        |> Signal.validate validation

    member this.Output = validation

    member __.Validation = validation

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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module IO =
    module Input =
        let create signal =
            Input(signal, id)
        let converted conversion signal =
            Input(signal, conversion)
        let validated validation signal=
            ValidatedInput(signal, id, validation)
        let convertedValidated conversion validation signal =
            ValidatedInput(signal, conversion, validation)
    module InOut =
        let create<'a> signal =
            new InOut<'a,'a>(signal, id)
        let converted conversion signal =
            new InOut<'a,'b>(signal, conversion)
        let validated validation signal =
            new ValidatedInOut<_,_,_>(signal, id, validation)
        let convertedValidated conversion validation signal =
            new ValidatedInOut<_,_,_>(signal, conversion, validation)

    module MutableInOut =
        let validated<'a> validation mutatable = 
            new MutatableInOut<'a,'a>(mutatable, id, validation)
        let convertedValidated conversion validation mutatable =
            new MutatableInOut<_,_>(mutatable, conversion, validation)
