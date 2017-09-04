namespace Gjallarhorn.Bindable.Internal

open Gjallarhorn
open Gjallarhorn.Bindable

/// Internal type used for tracking values by platform specific binding sources
type IValueHolder =
    /// Get a boxed value
    abstract member GetValue : unit -> obj
    /// Set a boxed value
    abstract member SetValue : obj -> unit
    /// True if this represents a readonly binding
    abstract member ReadOnly : bool

/// Contains routines for creating value holders
module ValueHolder =
    /// Create a read-write value holder from delegates
    let readWrite (getter : System.Func<'a>) (setter : System.Action<'a>) = 
        { 
            new IValueHolder with 
                member __.GetValue() = box <| getter.Invoke()
                member __.SetValue(v) = setter.Invoke(unbox(v))
                member __.ReadOnly = false    
        }

    /// Create a read-only value holder from a delegate
    let readOnly (getValue : System.Func<'a>) = 
        { 
            new IValueHolder with 
                member __.GetValue() = box <| getValue.Invoke()
                member __.SetValue(_) = ()
                member __.ReadOnly = true
        }    

