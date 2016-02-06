namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Internal

open System
open System.Windows.Input

/// An ICommand which acts as a Signal over changes to the value.  This is frequently the current timestamp of the command.
type ITrackingCommand<'a> =
    inherit ICommand 
    inherit System.IDisposable
    inherit ISignal<'a>
    
/// Command type which uses an ISignal<bool> to track whether it can execute, and implements ISignal<'a> with the command parameter each time the command updates
/// Note that this will signal for each execution, whether or not the value has changed.
type ParameterCommand<'a> (initialValue : 'a, allowExecute : ISignal<bool>) as self =
    inherit SignalBase<'a>([| |])

    let canExecuteChanged = new Event<EventHandler, EventArgs>()
    let disposeTracker = new CompositeDisposable()
    let mutable lastValue = initialValue

    do
        allowExecute
        |> Signal.Subscription.create (fun _ -> self.RaiseCanExecuteChanged())    
        |> disposeTracker.Add    

    member this.RaiseCanExecuteChanged() =
        canExecuteChanged.Trigger(this, EventArgs.Empty)

    /// Used to process the command itself
    abstract member HandleExecute : obj -> unit
    default this.HandleExecute(param : obj) =
        let v = Utilities.downcastAndCreateOption param
        match v with
        | Some newVal ->
            lastValue <- newVal
            this.Signal()
        | None ->
            ()

    override __.Value with get() = lastValue
    override __.RequestRefresh _ = ()
    override __.OnDisposing () = disposeTracker.Dispose()

    interface ITrackingCommand<'a>     
    interface ICommand with
        [<CLIEvent>]
        member __.CanExecuteChanged = canExecuteChanged.Publish
        member __.CanExecute (_ : obj) = allowExecute.Value
        member this.Execute(param : obj) = this.HandleExecute(param)
   
/// Reports whether a command is executed, including the timestamp of the most recent execution
type CommandState =
    /// The command is in an unexecuted state
    | Unexecuted
    /// The command has been executed at a specific time
    | Executed of timestamp : DateTime

/// Command type which uses an ISignal<bool> to track whether it can execute, and implements ISignal<CommandState>, where each execute passes DateTime.UtcNow on execution
type SignalCommand (allowExecute : ISignal<bool>) =
    inherit ParameterCommand<CommandState>(Unexecuted, allowExecute)

    override __.HandleExecute _ =
        base.HandleExecute(Executed(DateTime.Now))

/// Reports whether a paramterized command is executed, including the timestamp of the most recent execution
type CommandParameterState<'a> =
    /// The command is in an unexecuted state
    | Unexecuted
    /// The command has been executed at a specific time with a specific argument
    | Executed of timestamp : DateTime * parameter : 'a    

/// Command type which uses an ISignal<bool> to track whether it can execute, and implements ISignal<DateTime>, where each execute passes DateTime.UtcNow on execution
type SignalParameterCommand<'a> (allowExecute : ISignal<bool>) =
    inherit ParameterCommand<CommandParameterState<'a>>(Unexecuted, allowExecute)

    override __.HandleExecute p =
        base.HandleExecute(Executed(DateTime.Now, p))

/// Core module for creating and using ICommand implementations
module Command =        
    /// Create a command with an optional enabling source, provided as an ISignal<bool>
    let create enabledSource =
        (new SignalCommand(enabledSource)) :> ITrackingCommand<CommandState>

    /// Create a command which is always enabled
    let createEnabled () =
        create (Signal.constant true)

    /// Create a subscription to the changes of a command which calls the provided function upon each change
    let subscribe (f : DateTime -> unit) (provider : ITrackingCommand<CommandState>) = 
        let f state =
            match state with
            | CommandState.Executed(time) -> f(time)
            | _ -> ()
        
        Signal.Subscription.create f provider