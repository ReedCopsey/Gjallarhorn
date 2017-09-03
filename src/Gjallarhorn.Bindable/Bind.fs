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

module Bind =
    /// Add a watched signal (one way property) to a binding source by name
    let oneWay<'Model, 'Prop, 'Msg> (getter : 'Model -> 'Prop) (name : Expr<'Prop>) : BindingSource -> ISignal<'Model> -> IObservable<'Msg> option =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let mapped = signal |> Signal.map getter
            source.TrackInput (getPropertyNameFromExpression name, IO.Report.create mapped)
            None

    /// Add a watched signal (one way validated property) to a binding source by name
    let oneWayValidated<'Model, 'Prop, 'Msg> (getter : 'Model -> 'Prop) validation (name : Expr<'Prop>) : BindingSource -> ISignal<'Model> -> IObservable<'Msg> option =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let mapped = signal |> Signal.map getter
            source.TrackInput (getPropertyNameFromExpression name, IO.Report.validated validation mapped)      
            None

    /// Add a two way property to a binding source by name
    let twoWay<'Model, 'Prop, 'Msg> (getter : 'Model -> 'Prop) (setter : 'Prop -> 'Msg) (name : Expr<'Prop>) : BindingSource -> ISignal<'Model> -> IObservable<'Msg> option =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let name = getPropertyNameFromExpression name
            let mapped = signal |> Signal.map getter
            let output = Binding.toFromView<'Prop> source name mapped
            output
            |> Observable.map (setter)
            |> Some

    /// Add a two way property to a binding source by name
    let twoWayValidated<'Model, 'Prop, 'Msg> (getter : 'Model -> 'Prop) (validation : Validation<'Prop,'Prop>) (setter : 'Prop -> 'Msg) (name : Expr<'Prop>) : BindingSource -> ISignal<'Model> -> IObservable<'Msg> option =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let name = getPropertyNameFromExpression name
            let mapped = signal |> Signal.map getter
            let validated = Binding.toFromViewValidated<'Prop,'Prop> source name validation mapped
            validated
            |> Observable.toMessage (setter)
            |> Some

    /// Creates an ICommand (one way property) to a binding source by name which outputs a specific message
    let cmd<'Model,'Msg> (name : Expr<Cmd<'Msg>>) : BindingSource -> ISignal<'Model> -> IObservable<'Msg> option =
        fun (source : BindingSource) (_signal : ISignal<'Model>) ->
            let o, pi = getPropertyFromExpression name
            match o.Value with
            | PropertyGet(_,v,_) ->
                let msg = pi.GetValue(v.GetValue(null)) :?> Cmd<'Msg>
                Binding.createMessage pi.Name msg.Value source
            | _ -> failwith "Bad expression"        
            |> Some

    /// Creates an ICommand (one way property) to a binding source by name which outputs a specific message
    let cmdIf<'Model,'Msg> canExecute (name : Expr<Cmd<'Msg>>) : BindingSource -> ISignal<'Model> -> IObservable<'Msg> option =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let o, pi = getPropertyFromExpression name
            match o.Value with
            | PropertyGet(_,v,_) ->
                let msg = pi.GetValue(v.GetValue(null)) :?> Cmd<'Msg>
                let canExecuteSignal = signal |> Signal.map canExecute
                Binding.createMessageChecked pi.Name canExecuteSignal msg.Value source
            | _ -> failwith "Bad expression"        
            |> Some

    /// Bind a component as a two-way property, acting as a reducer for messages from the component
    let comp<'Model,'Msg,'Submodel,'Submsg> (getter : 'Model -> 'Submodel) (componentVm : Component<'Submodel, 'Submsg>) (mapper : 'Submsg * 'Submodel -> 'Msg) (name : Expr<'Submodel>) =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let name = getPropertyNameFromExpression name
            let mapped = signal |> Signal.map getter
            let output = Binding.componentToView source name componentVm mapped 
            output
            |> Observable.map (fun subMsg -> mapper(subMsg, mapped.Value))
            |> Some  
       
    let collection<'Model,'Msg,'Submodel,'Submsg when 'Submodel : equality> (getter : 'Model -> 'Submodel seq) (collectionVm : Component<'Submodel, 'Submsg>) (mapper : 'Submsg * 'Submodel -> 'Msg) (name : Expr<'Submodel seq>) =
        fun (source : BindingSource) (signal : ISignal<'Model>) ->
            let name = getPropertyNameFromExpression name
            let mapped = signal |> Signal.map getter
            let output = BindingCollection.toView source name mapped collectionVm
            output
            |> Observable.map mapper
            |> Some

    let route<'Model,'Msg> (source : BindingSource) (model : ISignal<'Model>) (list : (BindingSource -> ISignal<'Model> -> IObservable<'Msg> option) list) : IObservable<'Msg> list =
        list
        |> List.choose (fun v -> v source model)

