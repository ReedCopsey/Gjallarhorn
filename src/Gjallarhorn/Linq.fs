namespace Gjallarhorn.Linq

open Gjallarhorn
open Gjallarhorn.Helpers
open Gjallarhorn.Internal
open Gjallarhorn.Validation

open System
open System.Runtime.CompilerServices

[<AbstractClass;Sealed;Extension>]
type SignalExtensions() =

    [<Extension>]
    static member Select<'a,'b> (this:ISignal<'a>, mapper:Func<'a,'b>) =
        this
        |> Signal.map mapper.Invoke 