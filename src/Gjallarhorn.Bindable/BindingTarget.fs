namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Validation
open System.ComponentModel
open System.Windows.Input

[<AbstractClass>]
type BindingTargetBase() as self =
    let propertyChanged = new Event<_, _>()
    let errorsChanged = new Event<_, _>()
    let operationExecuting = Mutable.create false
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
        (self :> IBindingTarget).TrackView "OperationExecuting" operationExecuting

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member __.PropertyChanged = propertyChanged.Publish

    /// Add a binding target for a mutatable value with a given name
    abstract BindMutable<'a> : string -> IMutatable<'a> -> unit
    
    /// Add a binding target for a view with a given name
    abstract BindView<'a> : string -> IView<'a> -> unit

    /// Add a binding target for a command with a given name
    abstract BindCommand : string -> ICommand -> unit

    member this.IsValid = isValid.Value

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
        member __.OperationExecuting with get() = operationExecuting.Value

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

     
module Bind =
    // let create () : To implement by each framework library

    let edit name mut (target : #IBindingTarget) =
        target.BindMutable name mut
        target
        
    let watch name view (target : #IBindingTarget) =
        target.BindView name view
        target

module Binding =    
    /// Custom computation expression builder for composing IView instances dynamically    
    type BindingBuilder(creator : unit -> IBindingTarget) =        
        member __.Zero() = creator()
        member __.Yield(_) = creator()
        [<CustomOperation("edit", MaintainsVariableSpace = true)>]
        member __.Edit (source : IBindingTarget, name, value) = Bind.edit name value source
        [<CustomOperation("watch", MaintainsVariableSpace = true)>]
        member __.Watch (source : IBindingTarget, name, view) = Bind.watch name view source        
