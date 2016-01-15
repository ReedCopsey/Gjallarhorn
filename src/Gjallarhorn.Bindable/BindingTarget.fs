namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Internal
open Gjallarhorn.Validation
open System.ComponentModel
open System.Windows.Input

type ExecutionTracker() =
    let handles = ResizeArray<_>()

    member private this.Signal () = SignalManager.Signal(this)
     
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

    // Mutable uses SignalManager to manage its dependencies (always)
    interface IView<bool> with
        member __.Value with get() = lock handles (fun _ -> handles.Count > 0)
        member this.AddDependency _ dep =            
            SignalManager.AddDependency this dep                
        member this.RemoveDependency _ dep =
            SignalManager.RemoveDependency this dep
        member this.Signal () = this.Signal()

[<AbstractClass>]
type BindingTargetBase() as self =
    let propertyChanged = new Event<_, _>()
    let errorsChanged = new Event<_, _>()
    let executionTracker = ExecutionTracker()
    let isValid = Mutable.create true

    let errors = System.Collections.Generic.Dictionary<string, string list>()

    let disposables = ResizeArray<System.IDisposable>()

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
    member __.Valid = isValid :> IView<bool>

    /// True when the current value is valid.  Can be used in bindings
    member __.IsValid = isValid.Value

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member __.PropertyChanged = propertyChanged.Publish

    /// Add a binding target for a mutatable value with a given name
    abstract BindMutable<'a> : string -> IMutatable<'a> -> unit
    
    /// Add a binding target for a view with a given name
    abstract BindView<'a> : string -> IView<'a> -> unit

    /// Add a binding target for a command with a given name
    abstract BindCommand : string -> ICommand -> unit

    interface INotifyDataErrorInfo with
        member __.GetErrors name =             
            match errors.TryGetValue name with
            | true, err -> err :> System.Collections.IEnumerable
            | false, _ -> [| |] :> System.Collections.IEnumerable

        member __.HasErrors = errors.Count > 0

        [<CLIEvent>]
        member this.ErrorsChanged = errorsChanged.Publish

    interface IBindingTarget with
        member this.IsValid = this.IsValid

        member __.RaisePropertyChanged name = raisePropertyChanged name
        member __.RaisePropertyChanged expr = raisePropertyChangedExpr expr
        member __.OperationExecuting with get() = (executionTracker :> IView<bool>).Value

        member this.BindMutable name value = this.BindMutable name value
        member this.BindView name view = this.BindView name view
        member this.BindCommand name command = this.BindCommand name command

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
        member __.Dispose() =
            disposables
            |> Seq.iter (fun d -> d.Dispose())            
            disposables.Clear()

/// Functions to work with binding targets     
module Bind =
    // let create () : To implement by each framework library

    /// Add an editor (two way property) to a binding target by name
    let edit name mut (target : #IBindingTarget) =
        target.BindMutable name mut
        target
       
    /// Add a watched view (one way property) to a binding target by name
    let watch name view (target : #IBindingTarget) =
        target.BindView name view
        target

    /// A computational expression builder for a binding target
    type Binding(creator : unit -> IBindingTarget) =        
        member __.Zero() = creator()
        member __.Yield(()) = creator()
        /// Add an editor (two way property) to a binding target by name
        [<CustomOperation("edit", MaintainsVariableSpace = true)>]
        member __.Edit (source : IBindingTarget, name, value) = edit name value source
        /// Add a watched view (one way property) to a binding target by name
        [<CustomOperation("watch", MaintainsVariableSpace = true)>]
        member __.Watch (source : IBindingTarget, name, view) = watch name view source                
