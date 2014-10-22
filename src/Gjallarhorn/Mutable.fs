namespace Gjallarhorn

/// Manages creation of mutable variables
module Mutable =
    
    /// Create a mutable variable wrapping an initial value
    [<CompiledName("Create")>]
    let create value = 
        Mutable(value) :> IMutatable<_>   

    /// Gets the value associated with the mutatable object
    [<CompiledName("Get")>]
    let get (mutatable : IMutatable<_>) = 
        mutatable.Value

    /// Sets the value associated with the mutatable object
    [<CompiledName("Set")>]
    let set (mutatable : IMutatable<_>) value = 
        mutatable.Value <- value
