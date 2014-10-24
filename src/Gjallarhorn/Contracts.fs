namespace Gjallarhorn

[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Gjallarhorn.Tests")>]
do ()

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


/// A type which depends on some IValueProvider
[<AllowNullLiteral>]
type IDependent =
    inherit System.IDisposable
    /// Signals the type that it should refresh its current value as one of it's dependencies has been updated
    abstract member RequestRefresh : IView<'a> -> unit

type IDisposableView<'a> =
    inherit IView<'a>
    inherit System.IDisposable

/// <summary>A contract for an IObservable<'a> which is also IDisposable</summary>
/// <remarks>Disposing is optional, but will cause the IObservable to stop tracking changes</remarks>
type IDisposableObservable<'a> =
    inherit System.IObservable<'a>
    inherit System.IDisposable