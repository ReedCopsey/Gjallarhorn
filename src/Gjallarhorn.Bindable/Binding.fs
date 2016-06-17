namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Helpers
open Gjallarhorn.Internal
open Gjallarhorn.Interaction
open Gjallarhorn.Validation

open Gjallarhorn.Bindable
open Gjallarhorn.Bindable.FSharp

open System.ComponentModel
open System.Windows.Input

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

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
        let getCreateObservableBindingSourceFunction<'a> () = (createObservableBindingFunction typeof<'a>) :?> FSharp.ObservableBindingSource<'a>

    /// Create a binding subject for the installed platform        
    let createObservableSource<'a>() = Implementation.getCreateObservableBindingSourceFunction<'a>()

    /// Create a binding source for the installed platform        
    let createSource () = Implementation.getCreateBindingSourceFunction()

    /// Bind a signal to the binding source using the specified name
    let toFromView<'a> (source : BindingSource) name signal =
        let edit = IO.InOut.create signal
        edit |> source.AddDisposable
        edit |> source.TrackInOut<'a,'a,'a> name
        edit.UpdateStream

    /// Add a signal as an editor with validation, bound to a specific name
    let toFromViewValidated<'a,'b> (source : BindingSource) name (validator : Validation<'a,'b>) signal =
        let edit = IO.InOut.validated validator signal
        edit |> source.AddDisposable
        edit |> source.TrackInOut<'a,'a,'b> name
        edit.Output

    /// Add a signal as an editor with validation, bound to a specific name
    let toFromViewConvertedValidated<'a,'b,'c> (source : BindingSource) name (converter : 'a -> 'b) (validator : Validation<'b,'c>) signal =
        let edit = IO.InOut.convertedValidated converter validator signal
        edit |> source.AddDisposable
        edit |> source.TrackInOut<'a,'b,'c> name
        edit.Output

    /// Add a mutable as an editor, bound to a specific name
    let mutateToFromView (source : BindingSource) name mutatable =
        source.MutateToFromView (mutatable, name)

    /// Add a mutable as an editor with validation, bound to a specific name
    let mutateToFromViewValidated (source : BindingSource) name validator mutatable =
        source.MutateToFromView (mutatable, name, validator)

    /// Add a binding to a source for a signal for editing with a given property expression and validation, and returns a signal of the user edits
    let memberToFromView<'a,'b> (source : BindingSource) (expr : Expr) (validation : Validation<'a,'a>) (signal : ISignal<'b>) =
        let pi = 
            match expr with 
            | PropertyGet(_, pi, _) ->
                pi
            | _ -> failwith "Only quotations representing a lambda of a property getter can be used as an expression for EditMember"

        let mapped =
            signal
            |> Signal.map (fun b -> pi.GetValue(b) :?> 'a)
        toFromViewValidated<'a,'a> source pi.Name validation mapped

    /// Add a watched signal (one way property) to a binding source by name
    let toView (source : BindingSource) name signal =
        IO.Input.create signal
        |> source.TrackInput name

    /// Add a watched signal (one way property) to a binding source by name with validation
    let toViewValidated (source : BindingSource) name validation signal =
        IO.Input.validated validation signal
        |> source.TrackInput name

    /// Add a constant value (one way property) to a binding source by name
    let constantToView name value (source : BindingSource) =
        source.ConstantToView (value, name)

    /// Creates an ICommand (one way property) to a binding source by name
    let createCommand name (source : BindingSource) =
        source.CommandFromView name

    /// Creates a checked ICommand (one way property) to a binding source by name
    let createCommandChecked name canExecute (source : BindingSource) =
        source.CommandCheckedFromView (canExecute, name)
