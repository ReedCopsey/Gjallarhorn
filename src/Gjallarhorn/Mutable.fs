namespace Gjallarhorn

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