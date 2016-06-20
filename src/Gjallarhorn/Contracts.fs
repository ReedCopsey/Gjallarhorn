namespace Gjallarhorn.Internal

[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Gjallarhorn.Tests")>]
[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Gjallarhorn.Bindable")>]
do ()

/// Interface that allows a type to remotely add itself as a dependent
type ITracksDependents =
    abstract member Track : IDependent -> unit
    abstract member Untrack : IDependent -> unit
and 
    /// A type which depends on a signal
    [<AllowNullLiteral>] IDependent =    
    /// Signals the type that it should refresh its current value as one of it's dependencies has been updated
    abstract member RequestRefresh : obj -> unit

    /// Queries whether other dependencies are registered to this dependent
    abstract member HasDependencies : bool with get

namespace Gjallarhorn

/// Core interface for signals
/// <remarks>
/// All signals must also inherit Internal.IDependent and Internal.ITracksDependents.
/// </remarks>
type ISignal<'a> =
    inherit System.IObservable<'a>
    #if DEBUG
    // Debug only to force impelmentation in debug, but 
    // prevent these from showing up in C# intellisense, 
    // improving end-user usability 
    inherit Internal.ITracksDependents
    inherit Internal.IDependent
    #endif

    /// The current value of the type
    abstract member Value : 'a with get


/// Core interface for all mutatable types
type IMutatable<'a> =
    inherit ISignal<'a>
    
    /// The current value of the type
    abstract member Value : 'a with get, set
