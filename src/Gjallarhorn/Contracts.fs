namespace Gjallarhorn

[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Gjallarhorn.Tests")>]
[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Gjallarhorn.Bindable")>]
do ()

/// Used to define the type of tracking used.  This allows weak referencing to be supported as needed
type DependencyTrackingMechanism =
    /// Use the default mechanism preferred from the source view type
    | Default
    /// Force weak referencing to prevent the source from keeping a strong reference to the target
    | WeakReferenced

/// <summary>Core interface for all members which provide a current value
/// Dependent views can use this to query the current state
/// of the mutable value</summary>
type IView<'a> =
    /// The current value of the type
    abstract member Value : 'a with get

    /// Add a dependent to this view explicitly
    abstract member AddDependency : DependencyTrackingMechanism -> IDependent -> unit
    
    /// Remove a dependent from this view explicitly
    abstract member RemoveDependency : DependencyTrackingMechanism -> IDependent -> unit

    /// Signal to all dependents to refresh themselves
    abstract member Signal : unit -> unit
and 
    /// A type which depends on some IValueProvider
    [<AllowNullLiteral>] IDependent =
    inherit System.IDisposable
    /// Signals the type that it should refresh its current value as one of it's dependencies has been updated
    abstract member RequestRefresh : IView<'a> -> unit

/// Core interface for all mutatable types
type IMutatable<'a> =
    inherit IView<'a>
    
    /// The current value of the type
    abstract member Value : 'a with get, set

/// A view which implements IDisposable in order to stop tracking its source
type IDisposableView<'a> =
    inherit IView<'a>
    inherit System.IDisposable

/// <summary>A contract for an IObservable which is also IDisposable</summary>
/// <remarks>Disposing is optional, but will cause the IObservable to stop tracking changes</remarks>
type IDisposableObservable<'a> =
    inherit System.IObservable<'a>
    inherit System.IDisposable

