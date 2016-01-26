namespace Gjallarhorn

[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Gjallarhorn.Tests")>]
[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Gjallarhorn.Bindable")>]
do ()

/// Type used to track dependencies
type [<AllowNullLiteral>] IDependencyManager<'a> =
    /// Add a dependent to this view explicitly
    abstract member Add : IDependent -> unit

    /// Add a dependent observer to this view explicitly
    abstract member Add : System.IObserver<'a> -> unit
    
    /// Remove a dependent from this view explicitly
    abstract member Remove : IDependent -> unit

    /// Remove a dependent observer from this view explicitly
    abstract member Remove : System.IObserver<'a> -> unit

    /// Remove all dependencies from this view
    abstract member RemoveAll : unit -> unit

    /// Signal to all dependents to refresh themselves
    abstract member Signal : IView<'a> -> unit
and 
    /// A type which depends on some IValueProvider
    [<AllowNullLiteral>] IDependent =    
    /// Signals the type that it should refresh its current value as one of it's dependencies has been updated
    abstract member RequestRefresh : IView<'a> -> unit
and IView<'a> =
//     inherit System.IObservable<'a>
    /// The current value of the type
    abstract member Value : 'a with get

    /// Get the dependency manager responsible for managing this view
    abstract member DependencyManager : IDependencyManager<'a> with get

/// Core interface for all mutatable types
type IMutatable<'a> =
    inherit IView<'a>
    
    /// The current value of the type
    abstract member Value : 'a with get, set

/// A view which implements IDisposable in order to stop tracking its source
type IDisposableView<'a> =
    inherit IView<'a>
    inherit System.IDisposable

/// A mutatable which implements IDisposable in order to stop tracking its source
type IDisposableMutatable<'a> =
    inherit IMutatable<'a>
    inherit System.IDisposable

/// <summary>A contract for an IObservable which is also IDisposable</summary>
/// <remarks>Disposing is optional, but will cause the IObservable to stop tracking changes</remarks>
type IDisposableObservable<'a> =
    inherit System.IObservable<'a>
    inherit System.IDisposable

