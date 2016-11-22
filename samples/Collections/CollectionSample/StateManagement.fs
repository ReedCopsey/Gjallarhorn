namespace CollectionSample

open Gjallarhorn


module StateManagement =
    // Initialization function - Kick off our routines to add and remove data
    let initExternalUpdates fnAccepted fnRejected =
        External.ExternalUpdater.startUpdatingLoop ()
        External.ExternalUpdater.startProcessingLoop fnAccepted fnRejected

    // Function to update the current state
    let update = State.Update >> State.stateManager.Post 

    // Functions to get the current state
    let get () = State.stateManager.PostAndReply State.Get
    let getAsync () = State.stateManager.PostAndAsyncReply State.Get

    // Gets the state as a Signal
    let asSignal () =       
        let current = get ()  
        Signal.fromObservable current State.stateChanged 
