namespace Gjallarhorn.Control

open Gjallarhorn
open Gjallarhorn.Internal

open System

/// Provides mechanisms for editing IMutatable instances
module Edit =
    /// Transforms a view value by using a specified mapping function.
    let map (viewMapping : 'a -> 'b) (setMapping : 'b -> 'a) (provider : IMutatable<'a>) = 
        new MappingEditor<'a,'b>(provider, viewMapping, setMapping, false) :> IMutatable<'b>