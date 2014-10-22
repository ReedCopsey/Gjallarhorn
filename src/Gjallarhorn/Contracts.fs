namespace Gjallarhorn

/// <summary>Core interface for all members which provide a current value
/// Dependent views can use this to query the current state
/// of the mutable value</summary>
type IView<'a> =
    /// The current value of the type
    abstract member Value : 'a with get

/// Core interface for all mutatable types
type IMutatable<'a> =
    inherit IView<'a>
    
    /// The current value of the type
    abstract member Value : 'a with get, set


[<AllowNullLiteral>]
/// A type which depends on some IValueProvider
type IDependent =
    /// Signals the type that it should refresh its current value as one of it's dependencies has been updated
    abstract member RequestRefresh : IView<'a> -> unit
