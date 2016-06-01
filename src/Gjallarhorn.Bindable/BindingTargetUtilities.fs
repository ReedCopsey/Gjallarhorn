namespace Gjallarhorn.Bindable.Internal

open Gjallarhorn
open Gjallarhorn.Bindable

type IValueHolder =
    abstract member GetValue : unit -> obj
    abstract member SetValue : obj -> unit
    abstract member ReadOnly : bool

module ValueHolder =
    let readWrite getter setter = 
        { 
            new IValueHolder with 
                member __.GetValue() = box <| getter()
                member __.SetValue(v) = setter(unbox(v))
                member __.ReadOnly = false    
        }
    let readOnly getValue = 
        { 
            new IValueHolder with 
                member __.GetValue() = box <| getValue()
                member __.SetValue(_) = ()
                member __.ReadOnly = true
        }    

