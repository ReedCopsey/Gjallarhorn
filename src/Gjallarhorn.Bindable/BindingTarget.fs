namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Internal
open Gjallarhorn.Validation
open System.ComponentModel
open System.Windows.Input

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

    interface IView<bool> with
        member __.Value with get() = lock handles (fun _ -> handles.Count > 0)
        
[<AbstractClass>]
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

        (self :> IBindingTarget).TrackView "IsValid" isValid
        (self :> IBindingTarget).TrackView "OperationExecuting" executionTracker

    /// Used by commanding to track executing operations
    member __.ExecutionTracker = executionTracker

    /// An IView<bool> that is set to true while tracked commands execute
    member __.Executing = executionTracker :> IView<bool>

    /// An IView<bool> used to track the current valid state
    member __.Valid with get() = isValid :> IView<bool>

    /// True when the current value is valid.  Can be used in bindings
    member __.IsValid with get() = isValid.Value

    /// Track a disposable, and dispose it when we are disposed
    member __.TrackDisposable disposable = disposables.Add(disposable)

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member __.PropertyChanged = propertyChanged.Publish

    abstract AddReadOnlyProperty<'a> : string -> IView<'a> -> unit
    abstract AddReadWriteProperty<'a> : string -> IView<'a> -> IView<'a>
    abstract AddCommand : string -> ICommand -> unit

    member this.BindEditor<'a> name validator (view : IView<'a>) =
        bt().TrackView name view
        let result = this.AddReadWriteProperty name view

        let validated = 
            result
            |> View.validate validator

        bt().TrackValidator name validated.ValidationResult

        validated :> IView<'a>
    
    /// Add a binding target for a view with a given name
    member this.BindView<'a> name (view : IView<'a>) =
        bt().TrackView name view
        this.AddReadOnlyProperty name view
        match view with
        | :? Validation.IValidatedView<'a> as validator ->
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
        member __.OperationExecuting with get() = (executionTracker :> IView<bool>).Value

        member this.BindEditor name validator view = this.BindEditor name validator view 
        member this.BindView name view = this.BindView name view
        member this.BindCommand name command = this.BindCommand name command
        member this.TrackDisposable disposable = this.TrackDisposable disposable

        member __.TrackView name view =
            view
            |> View.subscribe (fun _ -> raisePropertyChanged name)
            |> disposables.Add

        member __.TrackValidator name validator =
            validator
            |> View.subscribe (fun result -> updateErrors name result)
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
       
    /// Add a watched view (one way property) to a binding target by name
    let watch name view (target : #IBindingTarget) =
        target.BindView name view
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
        /// Add a watched view (one way property) to a binding target by name
        [<CustomOperation("watch", MaintainsVariableSpace = true)>]
        member __.Watch (source : IBindingTarget, name, view) = watch name view source                
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
