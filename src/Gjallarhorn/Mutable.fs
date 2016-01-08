namespace Gjallarhorn

open Gjallarhorn.Internal
open Validation

/// Manages creation of mutable variables
module Mutable =
    
    [<CompiledName("Create")>]
    /// Create a mutable variable wrapping an initial value
    let create (value : 'a) = 
        Mutable(value) :> IMutatable<'a>

    [<CompiledName("Get")>]
    /// Gets the value associated with the mutatable object
    let get (mutatable : IMutatable<'a>) = 
        mutatable.Value

    [<CompiledName("Set")>]
    /// Sets the value associated with the mutatable object
    let set (mutatable : IMutatable<'a>) value = 
        mutatable.Value <- value

    /// Transforms a mutatable value for editing by using a specified mapping function for view and edit.
    let map (viewMapping : 'a -> 'b) (setMapping : 'b -> 'a) (provider : IMutatable<'a>) = 
        new MappingEditor<'a,'b>(provider, viewMapping, setMapping, false) :> IMutatable<'b>

    let step (viewMapping : 'a -> 'b) (stepFunction : 'a ->'b -> 'a) (provider : IMutatable<'a>) = 
        new SteppingEditor<'a,'b>(provider, viewMapping, stepFunction, false) :> IMutatable<'b>

    /// Transforms a mutatable value from one IConvertible type to another.
    let mapConvertible<'a,'b> (provider : IMutatable<'a>) =
        let conv a : 'T = System.Convert.ChangeType(a, typeof<'T>) :?> 'T
        let mut : IMutatable<'b> = map conv conv provider
        mut

    type internal ValidatorMapping<'a>(validator : ValidationCollector<'a> -> ValidationCollector<'a>, valueProvider : IMutatable<'a>) =
        inherit MappingEditor<'a,'a>(valueProvider, id, id, true)

        let validateCurrent () =
            validate valueProvider.Value
            |> validator
            |> Validation.result
        let validationResult = 
            validateCurrent()
            |> create

        let subscriptionHandle =
            let rec dependent =
                {
                    new IDependent with
                        member __.RequestRefresh _ =
                            validationResult.Value <- validateCurrent()
                    interface System.IDisposable with
                        member __.Dispose() = 
                            valueProvider.RemoveDependency DependencyTrackingMechanism.Default dependent
                }
            valueProvider.AddDependency DependencyTrackingMechanism.Default dependent
            dependent :> System.IDisposable

        member private __.EditAndValidate value =  
            validationResult.Value <- validateCurrent()
            value

        override __.Disposing() =
            subscriptionHandle.Dispose()
            SignalManager.RemoveAllDependencies validationResult

        interface IValidatedMutatable<'a> with
            member __.ValidationResult with get() = validationResult :> IView<ValidationResult<'a>>

            member __.IsValid = isValid validationResult.Value
            

    let validate<'a> (validator : ValidationCollector<'a> -> ValidationCollector<'a>) (value : IMutatable<'a>) =
        new ValidatorMapping<'a>(validator, value) :> IValidatedMutatable<'a>

    let createValidated<'a> (validator : ValidationCollector<'a> -> ValidationCollector<'a>) (initialValue : 'a) =
        create initialValue
        |> validate validator