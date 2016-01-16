namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Validation

open Microsoft.FSharp.Quotations

open System.ComponentModel
open System.Windows.Input

/// <summary>Extension of ICommand with a public method to fire the CanExecuteChanged event</summary>
/// <remarks>This type should provide a constructor which accepts an Execute (obj -> unit) and CanExecute (obj -> bool) function</remarks>
type INotifyCommand =
    inherit ICommand 
    
    /// Trigger the CanExecuteChanged event for this specific ICommand
    abstract RaiseCanExecuteChanged : unit -> unit

/// <summary>Extension of INotifyCommand with a public property to supply a CancellationToken.</summary>
/// <remarks>This allows the command to change the token for subsequent usages if required</remarks>
type IAsyncNotifyCommand =
    inherit INotifyCommand

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
    
    /// Add a binding target for a view with a given name
    abstract BindView<'a> : string -> IView<'a> -> unit

    /// Add a binding target for a command with a given name
    abstract BindCommand : string -> ICommand -> unit




