namespace Gjallarhorn.Bindable

open System.Windows.Input

/// Design time command implementation for ViewModel specifications
type VmCmd<'a>(msg: 'a) =
    member __.Value = msg
    interface ICommand with
        member __.CanExecute _ = false
        member __.Execute _ = ()
        member __.add_CanExecuteChanged _ = ()
        member __.remove_CanExecuteChanged _ = ()

module internal DesignData =
    let data =
        let d = [ 
            typeof<string>, box "DesignTimeString"
            typeof<float>, box 42.42
            typeof<int>, box 23
        ] 
        System.Linq.Enumerable.ToDictionary(d, fst, snd)

    let makeList () : 'a list = []
    let makeArray () : 'a array = [| |]
    let makeSeq () : 'a seq = Seq.empty

/// Utilities for generation of ViewModel types and instances
module Vm =
    /// Create a VmCmd (ICommand) for a VM type via a supplied Message
    let cmd msg = VmCmd msg
