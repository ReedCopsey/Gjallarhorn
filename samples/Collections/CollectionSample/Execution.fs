namespace CollectionSample.External

open System.Threading

/// The message which tells the executor to turn on or off
type SetExecutionState = | Executing of bool

/// The execution model type
type ExecutionStatus = 
    { 
        Operating : CancellationTokenSource option         
    }

/// Contains update function for managing execution
module Execution = 
    /// The update function, which takes the function to start the external operation, message, and current status
    let update (fn : unit -> CancellationTokenSource ) (Executing(upd)) (current : ExecutionStatus) = 
        match current.Operating, upd with
        | None, true -> 
            // Start the processing loop
            { current with Operating = Some <| fn () }
        | Some cts, false -> 
            // Stop the processing loop
            cts.Cancel()
            { current with Operating = None }
        | None, false 
        | Some _, true -> 
            current    

    