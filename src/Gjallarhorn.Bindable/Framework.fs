namespace Gjallarhorn.Bindable

open System
open Gjallarhorn
open Gjallarhorn.Bindable

module CoreFramework =
    let application<'Model,'Message> (model : 'Model) (update : 'Message -> 'Model -> 'Model) (viewBinding : ObservableBindingSource<'Message> -> ISignal<'Model> -> IObservable<'Message> list) (render : ObservableBindingSource<'Message> -> int ) =
        let state = Mutable.create model
        
        // Map our state directly into the view context - this gives us something that can be data bound
        let viewContext = 
            let source = Binding.createObservableSource<'Message>()        
            
            source.OutputObservables <| viewBinding source state
            source

        // Subscribe to the observables, and call our update function
        use _sub = viewContext.Subscribe (fun msg -> state.Value <- update msg state.Value)
        
        // Render the "application"
        render viewContext
        

