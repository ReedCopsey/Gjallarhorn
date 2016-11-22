namespace CollectionSample

open Gjallarhorn


module StateManagement =
    // Initialization function - Kick off our routines to add and remove data
    let initExternalUpdates fnAccepted fnRejected =
        External.ExternalUpdater.startUpdatingLoop ()
        External.ExternalUpdater.startProcessingLoop fnAccepted fnRejected

    // Gets the state as a Signal
    let asSignal () =       
        let current = State.get ()  
        Signal.fromObservable current State.stateChanged 
