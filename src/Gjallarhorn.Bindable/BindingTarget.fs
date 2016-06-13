namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System.ComponentModel
open System.Windows.Input

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
        
[<AbstractClass>]
/// Base class for binding targets, used by platform specific libraries to share implementation details
type BindingTargetBase<'b>() as self =
    let uiCtx = System.Threading.SynchronizationContext.Current
    let propertyChanged = new Event<_, _>()
    let errorsChanged = new Event<_, _>()
    let idleTracker = new IdleTracker(uiCtx)
    let isValid = Mutable.create true
    let output = Mutable.create Unchecked.defaultof<'b>

    let errors = System.Collections.Generic.Dictionary<string, string list>()

    let disposables = new CompositeDisposable()

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

    let updateErrors name (result : ValidationResult) =
        match errors.ContainsKey(name), result with
        | false, Valid -> 
            ()        
        | _, Invalid(err) -> 
            errors.[name] <- err
            raiseErrorNotifications name
            
        | true, Valid -> 
            errors.Remove(name) |> ignore
            raiseErrorNotifications name

    let updateValidState() = 
        isValid.Value <- errors.Count = 0

    let bt() =
        self :> IBindingTarget

    do
        errorsChanged.Publish.Subscribe (fun _ -> updateValidState())
        |> disposables.Add

        (self :> IBindingTarget).TrackObservable "IsValid" isValid
        (self :> IBindingTarget).TrackObservable "Idle" idleTracker
        (self :> IBindingTarget).TrackObservable "OperationExecuting" idleTracker

    /// Used by commanding to track executing operations
    member __.IdleTracker = idleTracker
    member __.OperationExecuting with get() = not (idleTracker :> ISignal<bool>).Value
    member __.Idle with get() = (idleTracker :> ISignal<bool>).Value

    /// An ISignal<bool> used to track the current valid state
    member __.Valid with get() = isValid :> ISignal<bool>

    /// True when the current value is valid.  Can be used in bindings
    member __.IsValid with get() = isValid.Value

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member __.PropertyChanged = propertyChanged.Publish

    abstract AddReadOnlyProperty<'a> : string -> (unit -> 'a) -> unit
    abstract AddReadWriteProperty<'a> : string -> (unit -> 'a) -> ('a -> unit) -> unit
    
    interface INotifyDataErrorInfo with
        member __.GetErrors name =             
            match errors.TryGetValue name with
            | true, err -> err :> System.Collections.IEnumerable
            | false, _ -> [| |] :> System.Collections.IEnumerable

        member __.HasErrors = errors.Count > 0

        [<CLIEvent>]
        member __.ErrorsChanged = errorsChanged.Publish

    interface System.IObservable<'b> with
        member __.Subscribe obs = output.Subscribe(obs)

    interface IBindingTarget with
        member this.IsValid with get() = this.IsValid
        member this.Valid with get() = this.Valid

        member __.RaisePropertyChanged name = raisePropertyChanged name
        member this.OperationExecuting with get() = this.OperationExecuting
        member this.Idle with get() = this.Idle
        member this.IdleTracker with get() = this.IdleTracker

        member this.MutateToFromView<'a> (mutatable : IMutatable<'a>, name) = 
            bt().TrackObservable name mutatable
            this.AddReadWriteProperty name (fun _ -> mutatable.Value) (fun v -> mutatable.Value <- v)

        member this.ToFromView<'a> (signal,name) = 
            // make sure validation checks happen before edits are pushed
            match signal with
            | :? Validation.IValidatedSignal<'a> as validator ->
                (bt()).TrackValidator name validator.ValidationResult.Value validator.ValidationResult
            | _ -> ()

            let editSource = Mutable.create signal.Value
            Signal.Subscription.copyTo editSource signal
            |> disposables.Add 

            bt().TrackObservable name signal
            this.AddReadWriteProperty name (fun _ -> editSource.Value) (fun v -> editSource.Value <- v)

            editSource :> ISignal<'a>

        member this.MutateToFromView (mutatable, name, validation) =
            bt().TrackObservable name mutatable
            let validated =
                mutatable
                |> Signal.validate validation
            bt().TrackValidator name validated.ValidationResult.Value validated.ValidationResult
            this.AddReadWriteProperty name (fun _ -> mutatable.Value) (fun v -> mutatable.Value <- v)

        member this.MutateToFromView (mutatable, name, converter, validation) =
            let converted = Mutable.create (converter mutatable.Value)
            bt().TrackObservable name converted

            // Handle changes from our input observable
            mutatable
            |> Signal.map converter
            |> Signal.Subscription.copyTo converted
            |> disposables.Add

            // Do our validation
            let validated =
                converted
                |> Signal.validate validation
            bt().TrackValidator name validated.ValidationResult.Value validated.ValidationResult

            // Copy back to the input when appropriate
            validated.ValidationResult
            |> Signal.Subscription.create(fun v -> 
                if v.IsValidResult then 
                    mutatable.Value <- validated.Value)
            |> disposables.Add

            this.AddReadWriteProperty name (fun _ -> converted.Value) (fun v -> converted.Value <- v)

        member this.ToFromView (signal, name, conversion, validation) =
            let converted = Signal.map conversion signal
            let output = (this :> IBindingTarget).ToFromView (converted, name)
            let valid =
                output
                |> Signal.validate validation
            bt().TrackValidator name  valid.ValidationResult.Value  valid.ValidationResult
            valid 

        member this.ToFromView (signal, name, validation) =
            let output = (this :> IBindingTarget).ToFromView (signal, name)
            let valid =
                output
                |> Signal.validate validation
            bt().TrackValidator name  valid.ValidationResult.Value  valid.ValidationResult
            valid 

        member this.FilterValid signal =
            signal
            |> Signal.observeOn uiCtx
            |> Observable.filter (fun _ -> this.IsValid)

        member this.ToView<'a> (signal : ISignal<'a>, name) = 
            bt().TrackObservable name signal
            this.AddReadOnlyProperty name (fun _ -> signal.Value)
            match signal with
            | :? Validation.IValidatedSignal<'a> as validator ->
                (bt()).TrackValidator name validator.ValidationResult.Value validator.ValidationResult
            | _ -> ()

        member __.CommandFromView name =
            let command = Command.createEnabled()
            disposables.Add command
            bt().ConstantToView (command, name)
            command

        member __.CommandCheckedFromView (canExecute, name) =
            let command = Command.create canExecute
            disposables.Add command
            bt().ConstantToView (command, name)
            command

        member this.ConstantToView (value, name) = 
            this.AddReadOnlyProperty name (fun _ -> value)

        member __.AddDisposable disposable = 
            disposables.Add(disposable)

        member __.AddDisposable2<'a> (tuple : 'a * System.IDisposable) = 
            disposables.Add(snd tuple)
            fst tuple

        member __.ObservableToSignal<'a> (initial : 'a) (obs: System.IObservable<'a>) =            
            Signal.Subscription.fromObservable initial obs
            |> bt().AddDisposable2            

        member __.TrackObservable name observable =
            observable
            |> Observable.subscribe (fun _ -> raisePropertyChanged name)
            |> disposables.Add

        member this.TrackValidator name current validator =
            validator
            |> Signal.Subscription.create (fun result -> updateErrors name result)
            |> disposables.Add

            this.AddReadOnlyProperty (getErrorsPropertyName name) (fun _ -> validator.Value.AsList() )
            this.AddReadOnlyProperty (getValidPropertyName name) (fun _ -> validator.Value.IsValidResult )

            updateErrors name current 

    interface IBindingSubject<'b> with
        member __.OutputValue value = output.Value <- value

        member __.OutputObservable obs =
            let sub = obs.Subscribe(fun v -> output.Value <- v)
            disposables.Add sub

    interface System.IDisposable with
        member __.Dispose() = disposables.Dispose()

/// Functions to work with binding targets     
module Binding =
    module Implementation =
        let mutable private createBindingTargetFunction : unit -> obj = (fun _ -> failwith "Platform targets not installed")
        let mutable private createBindingSubjectFunction : System.Type -> obj = (fun _ -> failwith "Platform targets not installed")

        let installCreationFunction fBT fBS = 
            createBindingTargetFunction <- fBT
            createBindingSubjectFunction <- fBS

        let getCreateBindingTargetFunction () = createBindingTargetFunction() :?> IBindingTarget
        let getCreateBindingSubjectFunction<'a> () = (createBindingSubjectFunction typeof<'a>) :?> IBindingSubject<'a>

    /// Create a binding subject for the installed platform        
    let createSubject () = Implementation.getCreateBindingSubjectFunction<_>()

    /// Create a binding target for the installed platform        
    let createTarget () = Implementation.getCreateBindingTargetFunction()

    /// Bind a signal to the binding target using the specified name
    let toFromView (target : IBindingTarget) name signal =
        target.ToFromView (signal, name)

    /// Add a signal as an editor with validation, bound to a specific name
    let toFromViewValidated (target : IBindingTarget) name validator signal =
        target.ToFromView (signal, name, validator)

    /// Add a mutable as an editor, bound to a specific name
    let mutateToFromView (target : IBindingTarget) name mutatable =
        target.MutateToFromView (mutatable, name)

    /// Add a mutable as an editor with validation, bound to a specific name
    let mutateToFromViewValidated (target : IBindingTarget) name validator mutatable =
        target.MutateToFromView (mutatable, name, validator)

    /// Add a binding to a target for a signal for editing with a given property expression and validation, and returns a signal of the user edits
    let memberToFromView (target : IBindingTarget) expr (validation : ValidationCollector<'a> -> ValidationCollector<'a>) signal =
        let pi = 
            match expr with 
            | PropertyGet(_, pi, _) ->
                pi
            | _ -> failwith "Only quotations representing a lambda of a property getter can be used as an expression for EditMember"

        let mapped =
            signal
            |> Signal.map (fun b -> pi.GetValue(b) :?> 'a)
        target.ToFromView (mapped, pi.Name, validation)

    /// Add a watched signal (one way property) to a binding target by name
    let toView (target : IBindingTarget) name signal =
        target.ToView(signal, name)

    /// Add a constant value (one way property) to a binding target by name
    let constantToView name value (target : IBindingTarget) =
        target.ConstantToView (value, name)

    /// Creates an ICommand (one way property) to a binding target by name
    let createCommand name (target : IBindingTarget) =
        target.CommandFromView name

    /// Creates a checked ICommand (one way property) to a binding target by name
    let createCommandChecked name canExecute (target : IBindingTarget) =
        target.CommandCheckedFromView (canExecute, name)
