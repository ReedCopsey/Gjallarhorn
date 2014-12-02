namespace Gjallarhorn.Control

open Gjallarhorn
open Gjallarhorn.Internal

open System

/// Provides mechanisms for editing IMutatable instances
module Edit =
    /// Transforms a mutatable value for editing by using a specified mapping function for view and edit.
    let map (viewMapping : 'a -> 'b) (setMapping : 'b -> 'a) (provider : IMutatable<'a>) = 
        new MappingEditor<'a,'b>(provider, viewMapping, setMapping, false) :> IMutatable<'b>

    /// Transforms a mutatable value from one IConvertible type to another.
    let mapConvertible<'a,'b> (provider : IMutatable<'a>) =
        let conv a : 'T = System.Convert.ChangeType(a, typeof<'T>) :?> 'T
        let mut : IMutatable<'b> = map conv conv provider
        mut