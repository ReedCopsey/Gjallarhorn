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

    let updateErrors name (result : ValidationResult) =
        match errors.ContainsKey(name), result with
        | false, Valid -> 
            ()        
        | _, Invalid(err) -> 
            errors.[name] <- err
            errorsChanged.Trigger(self, DataErrorsChangedEventArgs(name))
            
        | true, Valid -> 
            errors.Remove(name) |> ignore
            errorsChanged.Trigger(self, DataErrorsChangedEventArgs(name))

    let updateValidState() = 
        isValid.Value <- errors.Count = 0

    let bt() =
        self :> IBindingTarget

    do
        errorsChanged.Publish.Subscribe (fun _ -> updateValidState())
        |> disposables.Add

        (self :> IBindingTarget).TrackObservable("IsValid", isValid)
        (self :> IBindingTarget).TrackObservable("Idle", idleTracker)
        (self :> IBindingTarget).TrackObservable("OperationExecuting", idleTracker)

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

        member this.BindDirect<'a> (name, mutatable : IMutatable<'a>) = 
            bt().TrackObservable(name, mutatable)
            this.AddReadWriteProperty name (fun _ -> mutatable.Value) (fun v -> mutatable.Value <- v)

        member this.Bind<'a> (name, signal) = 
            // make sure validation checks happen before edits are pushed
            match signal with
            | :? Validation.IValidatedSignal<'a> as validator ->
                (bt()).TrackValidator(name, validator.ValidationResult.Value, validator.ValidationResult)
            | _ -> ()

            let editSource = Mutable.create signal.Value
            Signal.Subscription.copyTo editSource signal
            |> disposables.Add 

            bt().TrackObservable(name, signal)
            this.AddReadWriteProperty name (fun _ -> editSource.Value) (fun v -> editSource.Value <- v)

            editSource :> ISignal<'a>

        member this.EditDirect (name, validation, mutatable) =
            bt().TrackObservable(name, mutatable)
            let validated =
                mutatable
                |> Signal.validate validation
            bt().TrackValidator(name, validated.ValidationResult.Value, validated.ValidationResult)
            this.AddReadWriteProperty name (fun _ -> mutatable.Value) (fun v -> mutatable.Value <- v)

        member this.Edit (name, validation, signal) =
            let output = (this :> IBindingTarget).Bind (name, signal)
            let validated =
                output
                |> Signal.validate validation
            bt().TrackValidator(name, validated.ValidationResult.Value, validated.ValidationResult)
            validated

        member this.FilterValid signal =
            signal
            |> Signal.observeOn uiCtx
            |> Observable.filter (fun _ -> this.IsValid)

        member this.Watch<'a> (name, signal : ISignal<'a>) = 
            bt().TrackObservable(name, signal)
            this.AddReadOnlyProperty name (fun _ -> signal.Value)
            match signal with
            | :? Validation.IValidatedSignal<'a> as validator ->
                (bt()).TrackValidator(name, validator.ValidationResult.Value, validator.ValidationResult)
            | _ -> ()

        member this.Constant (name, value) = 
            this.AddReadOnlyProperty name (fun _ -> value)

        member __.AddDisposable disposable = 
            disposables.Add(disposable)

        member __.AddDisposable2<'a> (tuple : 'a * System.IDisposable) = 
            disposables.Add(snd tuple)
            fst tuple

        member __.ObservableToSignal<'a> (initial : 'a, obs: System.IObservable<'a>) =            
            Signal.Subscription.fromObservable initial obs
            |> bt().AddDisposable2            

        member __.TrackObservable(name, observable) =
            observable
            |> Observable.subscribe (fun _ -> raisePropertyChanged name)
            |> disposables.Add

        member __.TrackValidator(name, current, validator) =
            validator
            |> Signal.Subscription.create (fun result -> updateErrors name result)
            |> disposables.Add

            updateErrors name current 

        member __.Command name =
            let command = Command.createEnabled()
            disposables.Add command
            bt().Constant (name, command)
            command

        member __.CommandChecked (name, canExecute) =
            let command = Command.create canExecute
            disposables.Add command
            bt().Constant (name, command)
            command

    interface IBindingSubject<'b> with
        member __.OutputValue value = output.Value <- value

        member __.OutputObservable obs =
            let sub = obs.Subscribe(fun v -> output.Value <- v)
            disposables.Add sub

    interface System.IDisposable with
        member __.Dispose() = disposables.Dispose()

/// Functions to work with binding targets     
module BindingTarget =
    module Internal =
        let mutable private createBindingTargetFunction : unit -> obj = (fun _ -> failwith "Platform targets not installed")
        let mutable private createBindingSubjectFunction : System.Type -> obj = (fun _ -> failwith "Platform targets not installed")

        let installCreationFunction fBT fBS = 
            createBindingTargetFunction <- fBT
            createBindingSubjectFunction <- fBS

        let getCreateBindingTargetFunction () = createBindingTargetFunction() :?> IBindingTarget
        let getCreateBindingSubjectFunction<'a> () = (createBindingSubjectFunction typeof<'a>) :?> IBindingSubject<'a>

    /// Create a binding target for the installed platform
    let create = Internal.getCreateBindingTargetFunction

    /// Bind a signal to the binding target using the specified name
    let bind (target : IBindingTarget) name signal =
        target.Bind (name, signal)

    /// Add a signal as an editor with validation, bound to a specific name
    let edit (target : IBindingTarget) name validator signal =
        target.Edit (name, validator, signal)

    /// Add a mutable as an editor with validation, bound to a specific name
    let editDirect (target : IBindingTarget) name validator mutatable =
        target.EditDirect (name, validator, mutatable)

    /// Add a binding to a target for a signal for editing with with a given property expression and validation, and returns a signal of the user edits
    let editMember (target : IBindingTarget) expr (validation : ValidationCollector<'a> -> ValidationCollector<'a>) signal =
        let pi = 
            match expr with 
            | PropertyGet(_, pi, _) ->
                pi
            | _ -> failwith "Only quotations representing a lambda of a property getter can be used as an expression for EditMember"

        let mapped =
            signal
            |> Signal.map (fun b -> pi.GetValue(b) :?> 'a)
        target.Edit (pi.Name, validation, mapped)

    /// Add a watched signal (one way property) to a binding target by name
    let watch (target : IBindingTarget) name signal =
        target.Watch (name, signal)

    /// Add a constant value (one way property) to a binding target by name
    let constant name value (target : IBindingTarget) =
        target.Constant (name, value)

    /// Add an ICommand (one way property) to a binding target by name
    let command name (command : ICommand) (target : IBindingTarget) =
        constant name command target

    module Builder =
        let private builderWatch name signal (target : #IBindingTarget) =
            watch target name signal
            target

        let private builderConstant name value (target : #IBindingTarget) =
            constant name value target
            target

        let private builderCommand name (command : ICommand) (target : #IBindingTarget) =
            builderConstant name command target
        
        /// A computational expression builder for a binding target
        type Binding(creator : unit -> IBindingTarget) =        
            member __.Zero() = creator()
            member __.Yield(()) = creator()
            /// Add a watched signal (one way property) to a binding target by name
            [<CustomOperation("watch", MaintainsVariableSpace = true)>]
            member __.Watch (source : IBindingTarget, name, signal) = builderWatch name signal source                
            /// Add a constant (one way property) to a binding target by name
            [<CustomOperation("constant", MaintainsVariableSpace = true)>]
            member __.Constant (source : IBindingTarget, name, comm) = builderConstant name comm source                
            /// Add a command (one way property) to a binding target by name
            [<CustomOperation("command", MaintainsVariableSpace = true)>]
            member __.Command (source : IBindingTarget, name, comm) = builderCommand name comm source                

            /// Dispose of an object when we're disposed
            [<CustomOperation("dispose", MaintainsVariableSpace = true)>]
            member __.Dispose (source : IBindingTarget, disposables : #seq<System.IDisposable>) = 
                disposables
                |> Seq.iter source.AddDisposable
                source

    /// Create and bind a binding target using a computational expression
    let binding = Builder.Binding(create)

    /// Add bindings to an existing binding target using a computational expression
    let extend target = Builder.Binding((fun _ -> target))

/// Functions to work with binding targets     
module BindingSubject =
    /// Create a binding subject for the installed platform        
    let create () = BindingTarget.Internal.getCreateBindingSubjectFunction<_>()

