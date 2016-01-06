namespace Gjallarhorn.Bindable

open Gjallarhorn
open System.ComponentModel
open System.Windows.Input

[<AbstractClass>]
type BindingTargetBase() as self =
    let propertyChanged = new Event<_, _>()
    let errorsChanged = new Event<_, _>()
    let operationExecuting = Mutable.create false

    let disposables = ResizeArray<System.IDisposable>()

    let raisePropertyChanged name =
        propertyChanged.Trigger(self, new PropertyChangedEventArgs(name))

    let raisePropertyChangedExpr expr =
        raisePropertyChanged <| getPropertyNameFromExpression expr

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
        member __.GetErrors _ = 
            // TODO
            Seq.empty :> System.Collections.IEnumerable

        member __.HasErrors 
            with get() = 
                // TODO
                false

        [<CLIEvent>]
        member this.ErrorsChanged = errorsChanged.Publish

    interface IBindingTarget with
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

    interface System.IDisposable with
        member __.Dispose() =
            disposables
            |> Seq.iter (fun d -> d.Dispose())            
            disposables.Clear()

     
module Bind =
    // let create () : To implement by each framework library

    let edit name mut (target : IBindingTarget) =
        target.BindMutable name mut
        target
        
    let watch name view (target : IBindingTarget) =
        target.BindView name view
        target
