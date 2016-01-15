namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Internal

open System
open System.Windows.Input

/// Simple Command implementation for ICommand and INotifyCommand
type SimpleCommand (execute : obj -> unit, canExecute : obj -> bool) =
    let canExecuteChanged = new Event<EventHandler, EventArgs>()

    interface ICommand with
        [<CLIEvent>]
        member __.CanExecuteChanged = canExecuteChanged.Publish

        member __.CanExecute(param : obj) =
            canExecute(param)

        member __.Execute(param : obj) =
            execute(param)

    interface INotifyCommand with
        member this.RaiseCanExecuteChanged() =
            canExecuteChanged.Trigger(this, EventArgs.Empty)
    
/// Command type which uses an IView<bool> to track whether it can execute, and implements IView<'a> with the command parameter each time the command updates
/// Note that this will signal for each execution, whether or not the value has changed.
type ViewParameterCommand<'a> (initialValue : 'a, allowExecute : IView<bool>) as self =
    let canExecuteChanged = new Event<EventHandler, EventArgs>()

    let mutable value = initialValue

    do
        allowExecute
        |> View.cache // Don't hold memory references to this...
        |> View.subscribe (fun b -> (self :> INotifyCommand).RaiseCanExecuteChanged())    
        |> ignore

    member private this.Signal () = SignalManager.Signal(this)

    abstract member HandleExecute : obj -> unit
    default this.HandleExecute(param : obj) =
        let v = Utilities.downcastAndCreateOption param
        match v with
        | Some newVal ->
            value <- newVal
            this.Signal()
        | None ->
            ()

    interface IView<'a> with
        member __.Value with get() = value
        member this.AddDependency _ dep =            
            SignalManager.AddDependency this dep                
        member this.RemoveDependency _ dep =
            SignalManager.RemoveDependency this dep
        member this.Signal () = this.Signal()            

    interface ICommand with
        [<CLIEvent>]
        member __.CanExecuteChanged = canExecuteChanged.Publish

        member __.CanExecute(_ : obj) =
            allowExecute.Value

        member this.Execute(param : obj) =
            this.HandleExecute(param)

    interface INotifyCommand with
        member this.RaiseCanExecuteChanged() =
            canExecuteChanged.Trigger(this, EventArgs.Empty)

/// Command type which uses an IView<bool> to track whether it can execute, and implements IView<DateTime>, where each execute passes DateTime.UtcNow on execution
type ViewCommand (allowExecute : IView<bool>) =
    inherit ViewParameterCommand<DateTime>(DateTime.MinValue, allowExecute)

    override __.HandleExecute(_) =
        base.HandleExecute(DateTime.UtcNow)
        
