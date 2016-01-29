namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Internal
open Gjallarhorn.Validation
open System.ComponentModel
open System.Windows.Input

/// Type which tracks execution, allowing commands to disable as needed
type ExecutionTracker() as self =
    let handles = ResizeArray<_>()

    let dependencies = Dependencies.create [| |] self

    member private this.Signal () = dependencies.Signal this
     
    member private this.AddHandle h =
        lock handles (fun _ ->
            handles.Add h
            this.Signal()            
        )
    member private this.RemoveHandle h =
        lock handles (fun _ ->
            if handles.Remove h then this.Signal()            
        )

    member this.GetExecutionHandle () =
        let rec handle = 
            { new System.IDisposable with
                member __.Dispose() =
                    this.RemoveHandle handle
            }
        this.AddHandle handle
        handle

    interface System.IObservable<bool> with
        member __.Subscribe obs = 
            dependencies.Add obs
            { 
                new System.IDisposable with
                    member __.Dispose() = dependencies.Remove obs
            }
    interface ITracksDependents with
        member __.Track dep = dependencies.Add dep
        member __.Untrack dep = dependencies.Remove dep

    interface IDependent with
        member __.RequestRefresh _ = ()
        member __.HasDependencies = dependencies.HasDependencies

    interface ISignal<bool> with
        member __.Value with get() = lock handles (fun _ -> handles.Count > 0)
        
[<AbstractClass>]
/// Base class for binding targets, used by platform specific libraries to share implementation details
type BindingTargetBase() as self =
    let propertyChanged = new Event<_, _>()
    let errorsChanged = new Event<_, _>()
    let executionTracker = ExecutionTracker()
    let isValid = Mutable.create true

    let errors = System.Collections.Generic.Dictionary<string, string list>()

    let disposables = new CompositeDisposable()

    let raisePropertyChanged name =
        propertyChanged.Trigger(self, new PropertyChangedEventArgs(name))

    let raisePropertyChangedExpr expr =
        raisePropertyChanged <| getPropertyNameFromExpression expr

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

        (self :> IBindingTarget).TrackSignal "IsValid" isValid
        (self :> IBindingTarget).TrackSignal "OperationExecuting" executionTracker

    /// Used by commanding to track executing operations
    member __.ExecutionTracker = executionTracker

    /// An ISignal<bool> that is set to true while tracked commands execute
    member __.Executing = executionTracker :> ISignal<bool>

    /// An ISignal<bool> used to track the current valid state
    member __.Valid with get() = isValid :> ISignal<bool>

    /// True when the current value is valid.  Can be used in bindings
    member __.IsValid with get() = isValid.Value

    /// Track a disposable, and dispose it when we are disposed
    member __.TrackDisposable disposable = disposables.Add(disposable)

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member __.PropertyChanged = propertyChanged.Publish

    abstract AddReadOnlyProperty<'a> : string -> ISignal<'a> -> unit
    abstract AddReadWriteProperty<'a> : string -> ISignal<'a> -> ISignal<'a>
    abstract AddCommand : string -> ICommand -> unit

    member this.BindEditor<'a> name validator (signal : ISignal<'a>) =
        bt().TrackSignal name signal
        let result = this.AddReadWriteProperty name signal

        let validated = 
            result
            |> Signal.validate validator

        bt().TrackValidator name validated.ValidationResult

        validated :> ISignal<'a>
    
    /// Add a binding target for a signal with a given name
    member this.BindSignal<'a> name (signal : ISignal<'a>) =
        bt().TrackSignal name signal
        this.AddReadOnlyProperty name signal
        match signal with
        | :? Validation.IValidatedSignal<'a> as validator ->
            (bt()).TrackValidator name validator.ValidationResult
        | _ -> ()

    /// Add a binding target for a command with a given name
    member this.BindCommand = this.AddCommand

    interface INotifyDataErrorInfo with
        member __.GetErrors name =             
            match errors.TryGetValue name with
            | true, err -> err :> System.Collections.IEnumerable
            | false, _ -> [| |] :> System.Collections.IEnumerable

        member __.HasErrors = errors.Count > 0

        [<CLIEvent>]
        member __.ErrorsChanged = errorsChanged.Publish

    interface IBindingTarget with
        member this.IsValid with get() = this.IsValid
        member this.Valid with get() = this.Valid

        member __.RaisePropertyChanged name = raisePropertyChanged name
        member __.RaisePropertyChanged expr = raisePropertyChangedExpr expr
        member __.OperationExecuting with get() = (executionTracker :> ISignal<bool>).Value

        member this.BindEditor name validator signal = this.BindEditor name validator signal 
        member this.BindSignal name signal = this.BindSignal name signal
        member this.BindCommand name command = this.BindCommand name command
        member this.TrackDisposable disposable = this.TrackDisposable disposable

        member __.TrackSignal name signal =
            signal
            |> Signal.Subscription.create (fun _ -> raisePropertyChanged name)
            |> disposables.Add

        member __.TrackValidator name validator =
            validator
            |> Signal.Subscription.create (fun result -> updateErrors name result)
            |> disposables.Add

            updateErrors name validator.Value

    interface System.IDisposable with
        member __.Dispose() = disposables.Dispose()

/// Functions to work with binding targets     
module Bind =
    let mutable private creationFunction : unit -> IBindingTarget = (fun _ -> failwith "Platform targets not installed")

    module Internal =
        let installCreationFunction f = creationFunction <- f

    let create () =
        creationFunction()    
       
    /// Add a watched signal (one way property) to a binding target by name
    let watch name signal (target : #IBindingTarget) =
        target.BindSignal name signal
        target

    /// Add a command (one way property) to a binding target by name
    let command name command (target : #IBindingTarget) =
        target.BindCommand name command
        target

    /// A computational expression builder for a binding target
    type Binding(creator : unit -> IBindingTarget) =        
        member __.Zero() = creator()
        member __.Yield(()) = creator()
//        /// Add an editor (two way property) to a binding target by name
//        [<CustomOperation("edit", MaintainsVariableSpace = true)>]
//        member __.Edit (source : IBindingTarget, name, value) = edit name value source
        /// Add a watched signal (one way property) to a binding target by name
        [<CustomOperation("watch", MaintainsVariableSpace = true)>]
        member __.Watch (source : IBindingTarget, name, signal) = watch name signal source                
        /// Add a command (one way property) to a binding target by name
        [<CustomOperation("command", MaintainsVariableSpace = true)>]
        member __.Command (source : IBindingTarget, name, comm) = command name comm source                

        /// Dispose of an object when we're disposed
        [<CustomOperation("dispose", MaintainsVariableSpace = true)>]
        member __.Dispose (source : IBindingTarget, disposable : #System.IDisposable) = 
            source.TrackDisposable disposable 
            source

    /// Create and bind a binding target using a computational expression
    let binding = Binding(create)

    /// Add bindings to an existing binding target using a computational expression
    let extend target = Binding((fun _ -> target))
