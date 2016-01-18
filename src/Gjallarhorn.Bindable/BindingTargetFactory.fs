namespace Gjallarhorn.Bindable

open Gjallarhorn

/// Type used for building a Binding Target from a View's context
[<AbstractClass>]
type BindingTargetFactory() as self =
    let target = lazy (self.Generate());

    /// Retrieves the binding target 
    member __.Value with get() = target.Force();

    /// Implemented by subclasses to generate a binding target
    abstract member Generate : unit -> IBindingTarget
