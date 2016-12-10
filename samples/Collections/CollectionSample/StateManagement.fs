namespace CollectionSample

open Gjallarhorn
open CollectionSample.RequestModel
open System.Threading
open CollectionSample.External

    

// Create an application wide model+ msg + update which composes 
// multiple models
type Model = { Requests : Requests ; ExternalModel : ExternalModel }
    
type Msg =
    | External of External.UpdateExternal    
    | Update of Operations.Update

// Type that allows us to manage the state external of the basic application framework.
type StateManagement (fnAccepted : Request -> unit , fnRejected : Request -> unit) =

    let updateRequests = Operations.update fnAccepted fnRejected 
    let updateExternal (c : Model) msg = c.ExternalModel.Updater.Update msg c.ExternalModel
    
    let extModel = { Updating = None ; Processing = None ; Updater = ExternalUpdater() } 

    let update (msg : Msg) (current : Model )= 
        match msg with
        | Msg.External m -> { current with ExternalModel = updateExternal current m }
        | Msg.Update u -> { current with Requests = updateRequests u current.Requests }

    let state = new State<Model,Msg>({ Requests = [] ; ExternalModel = extModel }, update)

    // Process the messages from our external updater
    let _subscription =
        extModel.Updater.Updates
        |> Observable.subscribe (fun msg -> state.Update (Update msg) |> ignore )

    // Gets the state as a Signal
    member __.ToSignal () = state :> ISignal<_> 

    // Initialization function - Kick off our routines to add and remove data
    member __.Initialize () =
        // Start updating and processing
        External.UpdateExternal.SetUpdating true |> External |> state.Update |> ignore
        External.UpdateExternal.SetProcessing true |> External |> state.Update  |> ignore

    // Our main update function 
    member __.Update with get () = state.Update >> ignore
