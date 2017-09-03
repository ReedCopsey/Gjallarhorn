namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Helpers
open Gjallarhorn.Internal
open Gjallarhorn.Interaction
open Gjallarhorn.Validation

open Gjallarhorn.Bindable

open System
open System.ComponentModel
open System.Windows.Input

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

type Cmd<'a>(msg: 'a) =
    member __.Value = msg
    interface ICommand with
        member __.CanExecute _ = false
        member __.Execute _ = ()
        member __.add_CanExecuteChanged _ = ()
        member __.remove_CanExecuteChanged _ = ()

module Cmd =
    let ofMsg msg = Cmd msg

[<AutoOpen>]
module internal RefHelpers =
    let getPropertyFromExpression(expr : Expr) = 
        match expr with 
        | PropertyGet(o, pi, _) ->
            o, pi
        | _ -> failwith "Only quotations representing a lambda of a property getter can be used as an expression"

    let getPropertyNameFromExpression(expr : Expr) = 
        let _, pi = getPropertyFromExpression expr
        pi.Name


/// Functions to work with binding sources     
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Binding =    

    /// Internal module used to manage the actual construction of binding sources
    module Implementation =
        let mutable private createBindingSourceFunction : unit -> obj = (fun _ -> failwith "Platform targets not installed")
        let mutable private createObservableBindingFunction : System.Type -> obj = (fun _ -> failwith "Platform targets not installed")

        /// Installs a platform specific binding source creation functions
        let installCreationFunction fBS fOBS = 
            createBindingSourceFunction <- fBS
            createObservableBindingFunction <- fOBS

        /// Retrieves the platform specific creation function 
        let getCreateBindingSourceFunction () = createBindingSourceFunction() :?> BindingSource
        
        /// Retrieves the platform specific creation function 
        let getCreateObservableBindingSourceFunction<'a> () = (createObservableBindingFunction typeof<'a>) :?> ObservableBindingSource<'a>

    /// Create a binding subject for the installed platform        
    let createObservableSource<'a>() = Implementation.getCreateObservableBindingSourceFunction<'a>()

    /// Create a binding source for the installed platform        
    let createSource () = Implementation.getCreateBindingSourceFunction()

    /// Bind a signal to the binding source using the specified name
    let toFromView<'a> (source : BindingSource) name (signal : ISignal<'a>) =
        let edit = IO.InOut.create signal
        edit |> source.AddDisposable
        source.TrackInOut<'a,'a,'a>(name, edit)
        edit.UpdateStream

    /// Add a signal as an editor with validation, bound to a specific name
    let toFromViewValidated<'a,'b> (source : BindingSource) name (validator : Validation<'a,'b>) signal =
        let edit = IO.InOut.validated validator signal
        edit |> source.AddDisposable
        source.TrackInOut<'a,'a,'b> (name, edit)
        edit.Output

    /// Add a signal as an editor with validation, bound to a specific name
    let toFromViewConvertedValidated<'a,'b,'c> (source : BindingSource) name (converter : 'a -> 'b) (validator : Validation<'b,'c>) signal =
        let edit = IO.InOut.convertedValidated converter validator signal
        edit |> source.AddDisposable
        source.TrackInOut<'a,'b,'c> (name, edit)
        edit.Output

    /// Add a mutable as an editor, bound to a specific name
    let mutateToFromView<'a> (source : BindingSource) name (mutatable : IMutatable<'a>) =
        source.TrackObservable (name, mutatable)
        source.AddReadWriteProperty (name, (fun _ -> mutatable.Value), fun v -> mutatable.Value <- v)

    /// Add a mutable as an editor with validation, bound to a specific name
    let mutateToFromViewValidated<'a> (source : BindingSource) name validator mutatable =
        let edit = IO.MutableInOut.validated validator mutatable
        source.TrackInOut<'a,'a,'a> (name, edit)
        edit |> source.AddDisposable
        ()

    /// Add a mutable as an editor with validation, bound to a specific name
    let mutateToFromViewConverted<'a,'b> (source : BindingSource) name (converter : 'a -> 'b) (validator: Validation<'b,'a>) mutatable =
        let edit = IO.MutableInOut.convertedValidated converter validator mutatable
        source.TrackInOut<'a,'b,'a> (name, edit)
        edit |> source.AddDisposable
        ()

    /// Add a binding to a source for a signal for editing with a given property expression and validation, and returns a signal of the user edits
    let memberToFromView<'a,'b> (source : BindingSource) (expr : Expr) (validation : Validation<'a,'a>) (signal : ISignal<'b>) =
        let _, pi = getPropertyFromExpression expr
        let mapped =
            signal
            |> Signal.map (fun b -> pi.GetValue(b) :?> 'a)
        toFromViewValidated<'a,'a> source pi.Name validation mapped

    /// Add a watched signal (one way property) to a binding source by name
    let toView (source : BindingSource) name signal =
        source.TrackInput (name, IO.Report.create signal)
        
    /// Add a watched signal (one way property) to a binding source by name with validation
    let toViewValidated (source : BindingSource) name validation signal =
        source.TrackInput (name, IO.Report.validated validation signal)        

    /// Add a constant value (one way property) to a binding source by name
    let constantToView name value (source : BindingSource) =
        source.ConstantToView (value, name)

    /// Bind a component to the given name
    let componentToView<'TModel, 'TMessage> (source : BindingSource) name (comp : Component<'TModel,'TMessage>) (signal : ISignal<'TModel>) =
        source.TrackComponent(name, comp, signal)

    /// Creates an ICommand (one way property) to a binding source by name
    let createCommand name (source : BindingSource) =
        let command = Command.createEnabled()
        source.AddDisposable command
        source.ConstantToView (command, name)
        command

    /// Creates a checked ICommand (one way property) to a binding source by name
    let createCommandChecked name canExecute (source : BindingSource) =
        let command = Command.create canExecute
        source.AddDisposable command
        source.ConstantToView (command, name)
        command    

    /// Creates an ICommand (one way property) to a binding source by name which outputs a specific message
    let createCommandParam<'a> name (source : BindingSource) =
        let command : ITrackingCommand<'a> = Command.createParamEnabled()
        source.AddDisposable command
        source.ConstantToView (command, name)
        command

    /// Creates a checked ICommand (one way property) to a binding source by name which outputs a specific message
    let createCommandParamChecked<'a> name canExecute (source : BindingSource) =
        let command : ITrackingCommand<'a> = Command.createParam canExecute
        source.AddDisposable command
        source.ConstantToView (command, name)
        command

    /// Creates an ICommand (one way property) to a binding source by name which outputs a specific message
    let createMessage name message (source : BindingSource) =
        let command = Command.createEnabled()
        source.AddDisposable command
        source.ConstantToView (command, name)
        command |> Observable.map (fun _ -> message)

    /// Creates an ICommand (one way property) to a binding source by name which outputs a specific message
    let createMessageNamed<'a> (name : Expr<Cmd<'a>>) (source : BindingSource) =
        let o, pi = getPropertyFromExpression name
        match o.Value with
        | PropertyGet(_,v,_) ->
            let msg = pi.GetValue(v.GetValue(null)) :?> Cmd<'a>
            createMessage pi.Name msg.Value source
        | _ -> failwith "Bad expression"

    /// Creates a checked ICommand (one way property) to a binding source by name which outputs a specific message
    let createMessageChecked name canExecute message (source : BindingSource) =
        let command = Command.create canExecute
        source.AddDisposable command
        source.ConstantToView (command, name)
        command |> Observable.map (fun _ -> message) 

    /// Creates an ICommand (one way property) to a binding source by name which outputs a specific message
    let createMessageParam name message (source : BindingSource) =
        let command = Command.createParamEnabled ()
        source.AddDisposable command
        source.ConstantToView (command, name)
        command |> Observable.map (fun p -> message p)

    /// Creates a checked ICommand (one way property) to a binding source by name which outputs a specific message
    let createMessageParamChecked name canExecute message (source : BindingSource) =
        let command = Command.createParam canExecute
        source.AddDisposable command
        source.ConstantToView (command, name)
        command |> Observable.map (fun p -> message p)

