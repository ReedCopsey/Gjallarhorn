namespace Gjallarhorn.Linq

open Gjallarhorn
open Gjallarhorn.Helpers
open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System
open System.ComponentModel
open System.Runtime.CompilerServices

[<AbstractClass;Sealed>]
type Mutable() =
    static member Create<'a> (value : 'a) =
        Mutable<'a>(value) :> IMutatable<'a>

[<AbstractClass;Sealed;Extension>]
type SignalExtensions() =

    [<Extension>]
    static member Select<'a,'b> (this:ISignal<'a>, mapper:Func<'a,'b>) =
        this
        |> Signal.map mapper.Invoke 
                
    [<Extension;EditorBrowsable(EditorBrowsableState.Never)>]
    static member SelectMany<'a,'b>(this:ISignal<'a>, mapper:Func<'a,ISignal<'b>>) =
        mapper.Invoke(this.Value)

    [<Extension;EditorBrowsable(EditorBrowsableState.Never)>]
    static member SelectMany<'a,'b,'c>(this:ISignal<'a>, mapper:Func<'a,ISignal<'b>>, selector:Func<'a,'b,'c>) : ISignal<'c> =
        let b' = mapper.Invoke(this.Value)
        Signal.map2 (fun a b -> selector.Invoke(a,b)) this b'
