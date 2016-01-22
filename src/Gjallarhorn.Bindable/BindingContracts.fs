namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Validation

open Microsoft.FSharp.Quotations

open System.ComponentModel
open System.Windows.Input

/// An ICommand which acts as a View over changes to the value.  This is frequently the current timestamp of the command.
type ITrackingCommand<'a> =
    inherit ICommand 
    inherit System.IDisposable
    inherit IView<'a>
    
/// <summary>Extension of INotifyCommand with a public property to supply a CancellationToken.</summary>
/// <remarks>This allows the command to change the token for subsequent usages if required</remarks>
type IAsyncNotifyCommand<'a> =
    inherit ITrackingCommand<'a>

    abstract member CancellationToken : System.Threading.CancellationToken with get, set

/// Interface used to manage a binding target
type IBindingTarget =
    inherit INotifyPropertyChanged
    inherit INotifyDataErrorInfo
    inherit System.IDisposable

    /// Property allowing us to track whether any validation errors currently exist on this target
    abstract member IsValid : bool

    /// Property allowing us to watch our validation state
    abstract member Valid : IView<bool>

    /// Adds a disposable to track
    abstract member TrackDisposable : System.IDisposable -> unit

    /// Trigger the PropertyChanged event for a specific property
    abstract RaisePropertyChanged : string -> unit

    /// Trigger the PropertyChanged event for a specific property
    abstract RaisePropertyChanged : Expr -> unit

    /// Track changes on a view to raise property changed events
    abstract TrackView<'a> : string -> IView<'a> -> unit

    /// Track changes on a view of validation results to raise proper validation events
    abstract TrackValidator : string -> IView<ValidationResult> -> unit

    /// Value used to notify view that an asynchronous operation is executing
    abstract OperationExecuting : bool with get

    /// Add a binding target for a mutatable value with a given name
    abstract BindMutable<'a> : string -> IMutatable<'a> -> unit
    
    /// Binds an editor to the target, which consists of an input view and validator, and returns the view of the user edits
    // abstract BindEditor<'a> : string -> IView<'a> -> (ValidationCollector<'a> -> ValidationCollector<'a>) -> IView<'a>

    /// Add a binding target for a view with a given name
    abstract BindView<'a> : string -> IView<'a> -> unit

    /// Add a binding target for a command with a given name
    abstract BindCommand : string -> ICommand -> unit




