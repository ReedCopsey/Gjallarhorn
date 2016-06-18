namespace Gjallarhorn.Bindable.Internal

open Gjallarhorn
open Gjallarhorn.Bindable

type IValueHolder =
    abstract member GetValue : unit -> obj
    abstract member SetValue : obj -> unit
    abstract member ReadOnly : bool

module ValueHolder =
    let readWrite (getter : System.Func<'a>) (setter : System.Action<'a>) = 
        { 
            new IValueHolder with 
                member __.GetValue() = box <| getter.Invoke()
                member __.SetValue(v) = setter.Invoke(unbox(v))
                member __.ReadOnly = false    
        }
    let readOnly (getValue : System.Func<'a>) = 
        { 
            new IValueHolder with 
                member __.GetValue() = box <| getValue.Invoke()
                member __.SetValue(_) = ()
                member __.ReadOnly = true
        }    

