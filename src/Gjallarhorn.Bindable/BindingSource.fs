namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Interaction
open Gjallarhorn.Helpers
open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System
open System.ComponentModel
open System.Windows.Input

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns


[<AbstractClass>]
/// Base class of binding sources
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
    
    /// Track changes on an observable to raise property changed events
    member this.TrackObservable<'a> (name : string, observable : IObservable<'a>) =
        observable
        |> Observable.subscribe (fun _ -> raisePropertyChanged name)
        |> this.AddDisposable

    member private this.AddTracking () =
        this.TrackObservable ("IsValid", isValid)
        this.TrackObservable ("Idle", idleTracker)
        this.TrackObservable ("OperationExecuting", idleTracker)

    /// Track changes on an observable of validation results to raise proper validation events, initialized with a starting validation result
    member private this.TrackValidator (name : string) (validator : ISignal<ValidationResult>)=
        validator
        |> Signal.Subscription.create (fun result -> updateErrors name result)
        |> this.AddDisposable

        this.AddReadOnlyProperty (getErrorsPropertyName name, fun _ -> validator.Value.ToList(true) )
        this.AddReadOnlyProperty (getValidPropertyName name,fun _ -> validator.Value.IsValidResult )

        updateErrors name validator.Value 

    /// Track an Input type
    member this.TrackInput (name, input : Report<'a,'b>) =
        this.TrackObservable (name, input.UpdateStream)
        this.AddReadOnlyProperty (name, Func<_>(input.GetValue))
        
        // If we're validated input, handle adding our validation information as well
        match input with
        | :? ValidatedReport<'a, 'b> as v ->
            this.TrackValidator name v.Validation.ValidationResult
        | _ -> ()

    /// Track an InOut type
    member this.TrackInOut<'a,'b,'c> (name, inout : InOut<'a,'b>) =
        this.TrackObservable (name, inout.UpdateStream)
        this.AddReadWriteProperty (name, Func<_>(inout.GetValue), Action<_>(inout.SetValue))

        match inout with
        | :? ValidatedInOut<'a, 'b, 'c> as v ->
            this.TrackValidator name v.Output.ValidationResult
        | _ -> ()

    /// Create a typed observable binding source
    abstract CreateObservableBindingSource<'a> : unit -> ObservableBindingSource<'a>

    /// Track a component in the view, given a signal representing the model and a name for binding
    member this.TrackComponent<'a,'b> (name, comp : Component<'a,'b>, model : ISignal<'a>) = 
        let source = this.CreateObservableBindingSource<'b>()
        this.TrackObservable (name, model)
        this.AddReadOnlyProperty(name, fun _ -> source)
        
        let obs = comp (source :> BindingSource) model
        source.OutputObservables(obs)

        source :> IObservable<_>

    /// Add a readonly binding source for a constant value with a given name    
    member this.ConstantToView (value, name) = 
        this.AddReadOnlyProperty(name, fun _ -> value)

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

    interface Helpers.ICompositeDisposable with
        member __.Add d = disposables.Add d
        member __.Remove d = disposables.Remove d

    interface System.IDisposable with
        member __.Dispose() = disposables.Dispose()

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member __.PropertyChanged = propertyChanged.Publish
    
    /// Adds a read only property, specified by name and getter, to the binding source
    abstract AddReadOnlyProperty<'a> : string * System.Func<'a> -> unit

    /// Adds a read and write property, specified by name, getter, and setter, to the binding source
    abstract AddReadWriteProperty<'a> : string * System.Func<'a> * System.Action<'a> -> unit

/// Base class for binding sources, used by platform specific libraries to share implementation details
and [<AbstractClass>] ObservableBindingSource<'Message>() =
    inherit BindingSource()
    
    // Use event as simple observable source
    let output = Event<'Message>()

    /// Outputs a value through it's observable implementation
    member __.OutputValue value = output.Trigger value

    /// Outputs values by subscribing to changes on an observable
    member this.OutputObservable<'Message> (obs : IObservable<'Message>) =
        let sub = obs.Subscribe output.Trigger
        this.AddDisposable sub

    /// Outputs values by subscribing to changes on a list of observables
    member this.OutputObservables<'Message> (obs : IObservable<'Message> list) : unit =        
        obs
        |> List.iter this.OutputObservable
    
    interface System.IObservable<'Message> with
        member __.Subscribe obs = output.Publish.Subscribe obs

/// A component takes a BindingSource and a Signal for a model and returns a list of observable messages
and Component<'Model,'Message> = BindingSource -> ISignal<'Model> -> IObservable<'Message> list
