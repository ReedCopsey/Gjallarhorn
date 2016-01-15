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

    /// Transforms a mutable value for editing by using a mapping function for the view and a step function for the edit
    let step (viewMapping : 'a -> 'b) (stepFunction : 'a ->'b -> 'a) (provider : IMutatable<'a>) = 
        new SteppingEditor<'a,'b>(provider, viewMapping, stepFunction, false) :> IMutatable<'b>

    /// Filters the mutable, so only values set which match the predicate are pushed and propogated onwards
    let filter (predicate : 'a -> bool)  (provider : IMutatable<'a>) = 
        new FilteredEditor<'a>(provider, predicate, false) :> IDisposableMutatable<'a>

    /// Transforms a mutatable value from one IConvertible type to another.
    let mapConvertible<'a,'b> (provider : IMutatable<'a>) =
        let conv a : 'T = System.Convert.ChangeType(a, typeof<'T>) :?> 'T
        let mut : IMutatable<'b> = map conv conv provider
        mut

    /// Creates a mutatable value which validates and filters by using a ValidationCollector
    let validate<'a> (validator : ValidationCollector<'a> -> ValidationCollector<'a>) (value : IMutatable<'a>) =
        new ValidatorMappingEditor<'a>(validator, value) :> IValidatedMutatable<'a>

    /// Creates a new mutatable value which validates and filters by using a ValidationCollector
    let createValidated<'a> (validator : ValidationCollector<'a> -> ValidationCollector<'a>) (initialValue : 'a) =
        create initialValue
        |> validate validator