namespace Gjallarhorn

/// Manages creation of mutable variables
module Mutable =
    
    [<CompiledName("Create")>]
    /// Create a mutable variable wrapping an initial value
    let create value = 
        Mutable(value) :> IMutatable<_>   

    [<CompiledName("Get")>]
    /// Gets the value associated with the mutatable object
    let get (mutatable : IMutatable<_>) = 
        mutatable.Value

    [<CompiledName("Set")>]
    /// Sets the value associated with the mutatable object
    let set (mutatable : IMutatable<_>) value = 
        mutatable.Value <- value
