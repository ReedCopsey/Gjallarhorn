namespace Gjallarhorn.Bindable

open Gjallarhorn

/// Type used for building a binding source from a View's context
[<AbstractClass>]
type BindingSourceFactory() as self =
    let source = lazy (self.Generate());

    /// Retrieves the binding source
    member __.Value with get() = source.Force();

    /// Implemented by subclasses to generate a binding source
    abstract member Generate : unit -> BindingSource
