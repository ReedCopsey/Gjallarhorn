namespace Gjallarhorn

open Gjallarhorn.Internal

/// Manages creation of mutable variables
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Mutable =
    /// Create a mutable variable wrapping an initial value
    let create (value : 'a) = 
        Mutable<'a>(value) :> IMutatable<'a>

    /// Gets the value associated with the mutatable object
    let get (mutatable : IMutatable<'a>) = 
        mutatable.Value

    /// Sets the value associated with the mutatable object
    let set (mutatable : IMutatable<'a>) value = 
        mutatable.Value <- value

    /// Updates the value associated with the mutatable object via a function that takes the original value
    let step (f : 'a -> 'a) (mutatable : IMutatable<'a>) = 
        mutatable.Value <- f(mutatable.Value)