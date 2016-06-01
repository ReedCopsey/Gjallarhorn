namespace Gjallarhorn.Bindable

open Gjallarhorn
open System.Reflection

// TODO: Create a default IReflectableType binding target for use with Xamarin Forms, Perspex, and other frameworks supporting IReflectableType
type DefaultBindingTarget<'b>() =
    inherit BindingTargetBase<'b>()

    override __.AddReadWriteProperty<'a> name (getter : unit -> 'a) (setter : 'a -> unit) =
        ()
    override __.AddReadOnlyProperty<'a> name (getter : unit -> 'a) =
        ()
    
    interface IReflectableType with 
        member this.GetTypeInfo() =
            null
