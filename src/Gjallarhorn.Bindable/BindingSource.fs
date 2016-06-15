namespace Gjallarhorn.Bindable.FSharp

open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Helpers
open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System
open System.ComponentModel
open System.Windows.Input

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns


[<AbstractClass>]
type BindingSource() as self =

    let uiCtx = System.Threading.SynchronizationContext.Current
    let propertyChanged = new Event<_, _>()
    let errorsChanged = new Event<_, _>()
    let idleTracker = new IdleTracker(uiCtx)

    let raisePropertyChanged name =
        propertyChanged.Trigger(self, new PropertyChangedEventArgs(name))
    
    let getErrorsPropertyName propertyName =
        propertyName + "-" + "Errors"
    let getValidPropertyName propertyName =
        propertyName + "-" + "IsValid"

    let raiseErrorNotifications name =
        errorsChanged.Trigger(self, DataErrorsChangedEventArgs(name))
        propertyChanged.Trigger(self, PropertyChangedEventArgs(getErrorsPropertyName name))
        propertyChanged.Trigger(self, PropertyChangedEventArgs(getValidPropertyName name))

    let isValid = Mutable.create true

    let errors = System.Collections.Generic.Dictionary<string, string list>()

    let disposables = new CompositeDisposable()

    let updateErrors name (result : ValidationResult) =
        match errors.ContainsKey(name), result with
        | false, ValidationResult.Valid -> 
            ()        
        | _, ValidationResult.Invalid(err) -> 
            errors.[name] <- err
            raiseErrorNotifications name
            
        | true, ValidationResult.Valid -> 
            errors.Remove(name) |> ignore
            raiseErrorNotifications name

    let updateValidState() = 
        isValid.Value <- errors.Count = 0
        
    do
        errorsChanged.Publish.Subscribe (fun _ -> updateValidState())
        |> disposables.Add
        self.AddTracking ()

    /// Adds a disposable to track
    member __.AddDisposable disposable = 
        disposables.Add(disposable)

    /// Adds a disposable to track from the second element of a tuple, and returns the first element.  Used with Signal subscription functions.
    member __.AddDisposable2<'a> (tuple : 'a * System.IDisposable) = 
        disposables.Add(snd tuple)
        fst tuple

    /// Value used to notify signal that an asynchronous operation is executing, as well as schedule that operations should execute    
    member __.IdleTracker = idleTracker

    /// Value used to notify signal that an asynchronous operation is executing
    member __.OperationExecuting with get() = not (idleTracker :> ISignal<bool>).Value

    /// Value used to notify the front end that we're idle
    member __.Idle with get() = (idleTracker :> ISignal<bool>).Value

    /// An ISignal<bool> used to track the current valid state
    member __.Valid with get() = isValid :> ISignal<bool>

    /// True when the current value is valid.  Can be used in bindings
    member  __.IsValid with get() = isValid.Value

    /// Trigger the PropertyChanged event for a specific property
    member __.RaisePropertyChanged name = raisePropertyChanged name
    
    /// Map an initial value and observable to a signal, and track the subscription as part of this source's lifetime
    abstract ObservableToSignal<'a> : 'a -> IObservable<'a> -> ISignal<'a>

    /// Track changes on an observable to raise property changed events
    member this.TrackObservable<'a> (name : string) (observable : IObservable<'a>) =
        observable
        |> Observable.subscribe (fun _ -> raisePropertyChanged name)
        |> this.AddDisposable

    member private this.AddTracking () =
        this.TrackObservable "IsValid" isValid
        this.TrackObservable "Idle" idleTracker
        this.TrackObservable "OperationExecuting" idleTracker

    /// Track changes on an observable of validation results to raise proper validation events, initialized with a starting validation result
    member this.TrackValidator (name : string) (current : ValidationResult) (validator : ISignal<ValidationResult>)=
        validator
        |> Signal.Subscription.create (fun result -> updateErrors name result)
        |> this.AddDisposable

        this.AddReadOnlyProperty (getErrorsPropertyName name) (fun _ -> validator.Value.ToList(true) )
        this.AddReadOnlyProperty (getValidPropertyName name) (fun _ -> validator.Value.IsValidResult )

        updateErrors name current 

    /// Add a readonly binding source for a signal with a given name
    abstract ToView<'a> : ISignal<'a> * string -> unit

    /// Add a readonly binding source for a signal with a given name and validation
    abstract ToView<'a> : ISignal<'a> * string * Validation<'a,'a> -> unit

    /// Add a readonly binding source for a constant value with a given name
    abstract ConstantToView<'a> : 'a * string -> unit

    /// Creates a new command given a binding name
    abstract CommandFromView : string -> ITrackingCommand<DateTime>

    /// Creates a new command given signal for tracking execution and a binding name 
    abstract CommandCheckedFromView : ISignal<bool> * string -> ITrackingCommand<DateTime>

    /// Add a binding source for a signal with a given name, and returns a signal of the user edits
    abstract ToFromView<'a> : ISignal<'a> * string -> ISignal<'a>

    /// Add a binding source for a signal for editing with a given name and validation, and returns a signal of the user edits
    abstract ToFromView<'a,'b> : ISignal<'a> * string * Validation<'a,'b> -> IValidatedSignal<'a, 'b>

    /// Add a binding source for a signal for editing with a given name, conversion function, and validation, and returns a signal of the user edits
    abstract ToFromView<'a,'b> : ISignal<'a> * string * ('a -> 'b) * Validation<'b,'a> -> IValidatedSignal<'b, 'a>

    /// Add a binding source for a mutable with a given name which directly pushes edits back to the mutable
    abstract MutateToFromView<'a> : IMutatable<'a> * string -> unit

    /// Add a binding source for a mutable for editing with a given name and validation which directly pushes edits back to the mutable
    abstract MutateToFromView<'a> : IMutatable<'a> * string * Validation<'a,'a> -> unit

    /// Add a binding source for a mutable for editing with a given name, converter, and validation which directly pushes edits back to the mutable
    abstract MutateToFromView<'a,'b> : IMutatable<'a> * string * ('a -> 'b) * Validation<'b,'a> -> unit

    /// Filter a signal to only output when we're valid
    member this.FilterValid signal =
        signal
        |> Signal.observeOn uiCtx
        |> Observable.filter (fun _ -> this.IsValid)

    interface INotifyDataErrorInfo with
        member __.GetErrors name =             
            match errors.TryGetValue name with
            | true, err -> err :> System.Collections.IEnumerable
            | false, _ -> [| |] :> System.Collections.IEnumerable

        member __.HasErrors = errors.Count > 0

        [<CLIEvent>]
        member __.ErrorsChanged = errorsChanged.Publish

    interface System.IDisposable with
        member __.Dispose() = disposables.Dispose()

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member __.PropertyChanged = propertyChanged.Publish

    interface IBindingSource

    abstract AddReadOnlyProperty<'a> : string -> (unit -> 'a) -> unit
    abstract AddReadWriteProperty<'a> : string -> (unit -> 'a) -> ('a -> unit) -> unit
        
[<AbstractClass>]
/// Base class for binding sources, used by platform specific libraries to share implementation details
type ObservableBindingSource<'b>() as self =
    inherit BindingSource()

    let output = Mutable.create Unchecked.defaultof<'b>

    override this.MutateToFromView<'a> (mutatable : IMutatable<'a>, name) = 
        this.TrackObservable name mutatable
        this.AddReadWriteProperty name (fun _ -> mutatable.Value) (fun v -> mutatable.Value <- v)

    override this.ToFromView<'a> (signal,name) = 
        let editSource = Mutable.create signal.Value
        Signal.Subscription.copyTo editSource signal
        |> this.AddDisposable

        this.TrackObservable name signal
        this.AddReadWriteProperty name (fun _ -> editSource.Value) (fun v -> editSource.Value <- v)

        editSource :> ISignal<'a>

    override this.MutateToFromView (mutatable, name, validation) =
        this.TrackObservable name mutatable
        let validated =
            mutatable
            |> Signal.validate validation
        this.TrackValidator name validated.ValidationResult.Value validated.ValidationResult
        this.AddReadWriteProperty name (fun _ -> mutatable.Value) (fun v -> mutatable.Value <- v)

    override this.MutateToFromView (mutatable, name, converter, validation) =
        let converted = Mutable.create (converter mutatable.Value)
        this.TrackObservable name converted

        // Handle changes from our input observable
        mutatable
        |> Signal.map converter
        |> Signal.Subscription.copyTo converted
        |> this.AddDisposable

        // Do our validation
        let validated =
            converted
            |> Signal.validate validation
        this.TrackValidator name validated.ValidationResult.Value validated.ValidationResult

        // Copy back to the input when appropriate
        validated.ValidationResult
        |> Signal.Subscription.create(fun v -> 
            if v.IsValidResult then 
                mutatable.Value <- Option.get validated.Value)
        |> this.AddDisposable

        this.AddReadWriteProperty name (fun _ -> converted.Value) (fun v -> converted.Value <- v)

    override this.ToFromView (signal, name, conversion, validation) =
        let converted = Signal.map conversion signal
        let output = this.ToFromView (converted, name)
        let valid =
            output
            |> Signal.validate validation
        this.TrackValidator name  valid.ValidationResult.Value  valid.ValidationResult
        valid 

    override this.ToFromView (signal, name, validation) =
        let output = this.ToFromView (signal, name)
        let valid =
            output
            |> Signal.validate validation
        this.TrackValidator name  valid.ValidationResult.Value  valid.ValidationResult
        valid 

    override this.ToView<'a> (signal : ISignal<'a>, name) = 
        this.TrackObservable name signal
        this.AddReadOnlyProperty name (fun _ -> signal.Value)

    override this.ToView (signal, name, validation) = 
        this.TrackObservable name signal            
        this.AddReadOnlyProperty name (fun _ -> signal.Value)

        let validated = Signal.validate validation signal
        (this).TrackValidator name validated.ValidationResult.Value validated.ValidationResult            

    override this.CommandFromView name =
        let command = Command.createEnabled()
        this.AddDisposable command
        this.ConstantToView (command, name)
        command

    override this.CommandCheckedFromView (canExecute, name) =
        let command = Command.create canExecute
        this.AddDisposable command
        this.ConstantToView (command, name)
        command

    override this.ConstantToView (value, name) = 
        this.AddReadOnlyProperty name (fun _ -> value)

    override this.ObservableToSignal<'a> (initial : 'a) (obs: System.IObservable<'a>) =            
        Signal.Subscription.fromObservable initial obs
        |> this.AddDisposable2            

    /// Outputs a value through it's observable implementation
    member __.OutputValue value = output.Value <- value

    /// Outputs values by subscribing to changes on an observable
    member this.OutputObservable (obs : IObservable<'b>) =
        let sub = obs.Subscribe(fun v -> output.Value <- v)
        this.AddDisposable sub

    interface IObservableBindingSource<'b> with
        member this.OutputValue value = this.OutputValue value
        member this.OutputObservable obs = this.OutputObservable obs
    
    interface System.IObservable<'b> with
        member __.Subscribe obs = output.Subscribe(obs)
