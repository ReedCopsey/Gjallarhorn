namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Helpers
open Gjallarhorn.Internal

open System
open System.Windows.Input
   
/// An ICommand which acts as a Signal over changes to the value.  This is frequently the current timestamp of the command.
type ITrackingCommand<'a> =
    inherit ICommand 
    inherit System.IDisposable
    inherit IObservable<'a>    
    
/// Command type which uses an ISignal<bool> to track whether it can execute, and implements IObservable<'a> 
/// with the command parameter each time the command updates.
type internal ParameterCommand<'a> (allowExecute : ISignal<bool>) as self =
    let source = Mutable Unchecked.defaultof<'a>
    
    let canExecuteChanged = new Event<EventHandler, EventArgs>()
    let disposeTracker = new CompositeDisposable()

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
            source.Value <- newVal
        | None ->
            ()

    interface IDisposable with
        member __.Dispose () = disposeTracker.Dispose()

    interface IObservable<'a> with
        member this.Subscribe obs = (source :> IObservable<'a>).Subscribe obs

    interface ITrackingCommand<'a>     
    interface ICommand with
        [<CLIEvent>]
        member __.CanExecuteChanged = canExecuteChanged.Publish
        member __.CanExecute (_ : obj) = allowExecute.Value
        member this.Execute(param : obj) = this.HandleExecute(param)

/// Command type which uses an ISignal<bool> to track whether it can execute, and implements ISignal<CommandState>, where each execute passes DateTime.UtcNow on execution
type internal SignalCommand (allowExecute : ISignal<bool>) =
    inherit ParameterCommand<DateTime>(allowExecute)

    override __.HandleExecute _ =
        base.HandleExecute (DateTime.Now)

/// Command type which uses an ISignal<bool> to track whether it can execute, and implements ISignal<DateTime>, where each execute passes DateTime.UtcNow on execution
type internal SignalParameterCommand<'a> (allowExecute : ISignal<bool>) =
    inherit ParameterCommand<DateTime * 'a>(allowExecute)

    override __.HandleExecute param =
        let v = Utilities.downcastAndCreateOption param
        match v with
        | Some newVal ->
            let v = (DateTime.Now, newVal)
            base.HandleExecute v
        | None ->
            ()        

/// Core module for creating and using ICommand implementations
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Command =        
    /// Create a command with an optional enabling source, provided as an ISignal<bool>
    let create enabledSource =
        (new SignalCommand(enabledSource)) :> ITrackingCommand<DateTime>

    /// Create a command which is always enabled
    let createEnabled () =
        create (Signal.constant true)