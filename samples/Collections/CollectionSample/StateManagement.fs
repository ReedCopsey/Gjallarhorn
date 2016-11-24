namespace CollectionSample

open Gjallarhorn
open CollectionSample.Model

// Type that allows us to manage the state external of the basic application framework.
type StateManagement (fnAccepted : Request -> unit , fnRejected : Request -> unit) =
    let update = Operations.update fnAccepted fnRejected 
    let state = new State<Requests,Operations.Update>([], update)

    // Gets the state as a Signal
    member __.ToSignal () = state :> ISignal<_> 

    // Initialization function - Kick off our routines to add and remove data
    member __.Initialize () =
        External.ExternalUpdater.startUpdatingLoop state
        External.ExternalUpdater.startProcessingLoop state 

    // Our main update function 
    member __.Update with get () = state.Update >> ignore
