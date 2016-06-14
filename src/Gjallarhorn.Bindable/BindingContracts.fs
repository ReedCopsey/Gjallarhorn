namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Helpers
open Gjallarhorn.Validation

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

open System
open System.ComponentModel

type Validation<'a,'b> = (ValidationCollector<'a> -> ValidationCollector<'b>)

/// Interface used to manage a binding source
type IBindingSource =
    inherit INotifyPropertyChanged
    inherit INotifyDataErrorInfo
    inherit System.IDisposable

    /// Property allowing us to track whether any validation errors currently exist on this source
    abstract member IsValid : bool

    /// Property allowing us to watch our validation state
    abstract member Valid : ISignal<bool>

    /// Adds a disposable to track
    abstract member AddDisposable : System.IDisposable -> unit

    /// Adds a disposable to track from the second element of a tuple, and returns the first element.  Used with Signal subscription functions.
    abstract member AddDisposable2<'a> : ('a * System.IDisposable) -> 'a    

    /// Map an initial value and observable to a signal, and track the subscription as part of this source's lifetime
    abstract ObservableToSignal<'a> : 'a -> IObservable<'a> -> ISignal<'a>

    /// Trigger the PropertyChanged event for a specific property
    abstract RaisePropertyChanged : string -> unit

    /// Track changes on an observable to raise property changed events
    abstract TrackObservable<'a> : string -> IObservable<'a> -> unit

    /// Track changes on an observable of validation results to raise proper validation events, initialized with a starting validation result
    abstract TrackValidator : string -> ValidationResult -> ISignal<ValidationResult> -> unit

    /// Value used to notify signal that an asynchronous operation is executing
    abstract OperationExecuting : bool with get

    /// Value used to notify the front end that we're idle
    abstract Idle : bool with get

    /// Value used to notify signal that an asynchronous operation is executing, as well as schedule that operations should execute
    abstract IdleTracker : IdleTracker with get
    
    /// Add a readonly binding source for a signal with a given name
    abstract ToView<'a> : ISignal<'a> * string -> unit

    /// Add a readonly binding source for a constant value with a given name
    abstract ConstantToView<'a> : 'a * string -> unit

    /// Creates a new command given a binding name
    abstract CommandFromView : string -> ITrackingCommand<CommandState>

    /// Creates a new command given signal for tracking execution and a binding name 
    abstract CommandCheckedFromView : ISignal<bool> * string -> ITrackingCommand<CommandState>

    /// Add a binding source for a signal with a given name, and returns a signal of the user edits
    abstract ToFromView<'a> : ISignal<'a> * string -> ISignal<'a>

    /// Add a binding source for a signal for editing with a given name and validation, and returns a signal of the user edits
    abstract ToFromView<'a,'b> : ISignal<'a> * string * Validation<'a,'b> -> IValidatedSignal<'b>

    /// Add a binding source for a signal for editing with a given name, conversion function, and validation, and returns a signal of the user edits
    abstract ToFromView<'a,'b> : ISignal<'a> * string * ('a -> 'b) * Validation<'b,'a> -> IValidatedSignal<'a>

    /// Add a binding source for a mutable with a given name which directly pushes edits back to the mutable
    abstract MutateToFromView<'a> : IMutatable<'a> * string -> unit

    /// Add a binding source for a mutable for editing with a given name and validation which directly pushes edits back to the mutable
    abstract MutateToFromView<'a> : IMutatable<'a> * string * Validation<'a,'a> -> unit

    /// Add a binding source for a mutable for editing with a given name, converter, and validation which directly pushes edits back to the mutable
    abstract MutateToFromView<'a,'b> : IMutatable<'a> * string * ('a -> 'b) * Validation<'b,'a> -> unit

    /// Filter a signal to only output when we're valid
    abstract FilterValid<'a> : ISignal<'a> -> IObservable<'a>

/// Interface used to manage a typed binding source which outputs changes via IObservable
type IObservableBindingSource<'b> =
    inherit IBindingSource
    inherit System.IObservable<'b>

    /// Outputs a value through it's observable implementation
    abstract member OutputValue : 'b -> unit

    /// Outputs values by subscribing to changes on an observable
    abstract member OutputObservable : IObservable<'b> -> unit
