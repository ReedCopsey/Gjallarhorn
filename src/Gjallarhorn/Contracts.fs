namespace Gjallarhorn

[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Gjallarhorn.Tests")>]
[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Gjallarhorn.Bindable")>]
do ()

/// Core interface for views
type IView<'a> =
    inherit System.IObservable<'a>
    inherit ITracksDependents
    inherit IDependent

    /// The current value of the type
    abstract member Value : 'a with get
and ITracksDependents =
    abstract member Track : IDependent -> unit
    abstract member Untrack : IDependent -> unit
and 
    /// A type which depends on some IValueProvider
    [<AllowNullLiteral>] IDependent =    
    /// Signals the type that it should refresh its current value as one of it's dependencies has been updated
    abstract member RequestRefresh : IView<'a> -> unit

    /// Queries whether other dependencies are registered to this dependent
    abstract member HasDependencies : bool with get


/// Core interface for all mutatable types
type IMutatable<'a> =
    inherit IView<'a>
    
    /// The current value of the type
    abstract member Value : 'a with get, set
