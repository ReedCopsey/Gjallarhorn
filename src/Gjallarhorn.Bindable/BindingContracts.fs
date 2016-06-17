namespace Gjallarhorn.Bindable

open Gjallarhorn
open Gjallarhorn.Helpers
open Gjallarhorn.Validation

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns

open System
open System.ComponentModel


type Validation<'a,'b> = (Validation.ValidationCollector<'a> -> Validation.ValidationCollector<'b>)

/// Interface used to manage a binding source
type IBindingSource =
    inherit INotifyPropertyChanged
    inherit INotifyDataErrorInfo
    inherit System.IDisposable


/// Interface used to manage a typed binding source which outputs changes via IObservable
type IObservableBindingSource<'b> =
    inherit IBindingSource
    inherit System.IObservable<'b>

    /// Outputs a value through it's observable implementation
    abstract member OutputValue : 'b -> unit

    /// Outputs values by subscribing to changes on an observable
    abstract member OutputObservable : IObservable<'b> -> unit
