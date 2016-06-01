namespace Gjallarhorn.Bindable.Xamarin

open Gjallarhorn
open Gjallarhorn.Bindable

open System
open System.Reflection

type DynamicParameterInfo(memberInfo, t, name) =
    inherit ParameterInfo()

    override __.ParameterType = t
    override __.Member = memberInfo
    override __.Name = name
    
type RefTypeBindingTarget<'b>() =
    inherit BindingTargetBase<'b>()

    override __.AddReadWriteProperty<'a> name (getter : unit -> 'a) (setter : 'a -> unit) =
        ()
    override __.AddReadOnlyProperty<'a> name (getter : unit -> 'a) =
        ()
    
    interface IReflectableType with 
        member this.GetTypeInfo() =
            null

