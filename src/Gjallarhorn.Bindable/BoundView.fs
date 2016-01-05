namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Internal
open System.ComponentModel
open System.Reflection

/// An IView<'a> bound to a property on a source. This uses INotifyPropertyChanged to update the view as needed
type BoundView<'a>(name, initialValue, source : INotifyPropertyChanged) =
    let value = Mutable.create initialValue        

    let getValue () =
        let pi = source.GetType().GetRuntimeProperty(name)
        match pi with
        | null -> None
        | prop ->
            let v = prop.GetValue(source)
            downcastAndCreateOption(v)

    let updateValue v =
        match v with 
        | None -> ()
        | Some v' ->
            value.Value <- v'

    let handler (pch : PropertyChangedEventArgs) =
        match pch.PropertyName with
        | n when n = name ->
            getValue() 
            |> updateValue
        | _ -> ()

    let subscription = 
        source.PropertyChanged.Subscribe handler

    member this.Signal () = SignalManager.Signal(this)

    interface IDisposableView<'a> with
        member __.Value with get() = value.Value
        member this.AddDependency _ dep =            
            SignalManager.AddDependency this dep                
        member this.RemoveDependency _ dep =
            SignalManager.RemoveDependency this dep
        member this.Signal () =
            this.Signal()
        
    interface System.IDisposable with
        member this.Dispose() =
            subscription.Dispose()            
            SignalManager.RemoveAllDependencies this
