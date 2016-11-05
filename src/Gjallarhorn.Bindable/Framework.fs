namespace Gjallarhorn.Bindable

open System
open Gjallarhorn
open Gjallarhorn.Bindable

module Framework =
    type ApplicationCore<'Model,'Message> = 
        {
            Model : 'Model 
            Update : 'Message -> 'Model -> 'Model
            Binding : ObservableBindingSource<'Message> -> ISignal<'Model> -> IObservable<'Message> list
        }

    let info model update binding = { Model = model ; Update = update ; Binding = binding }

    type ApplicationSpecification<'Model,'Message> = 
        { 
            Core : ApplicationCore<'Model,'Message>
            Render : ObservableBindingSource<'Message> -> int
        }
        with 
            member this.Model = this.Core.Model
            member this.Update = this.Core.Update
            member this.Binding = this.Core.Binding

    let application<'Model,'Message> (applicationInfo : ApplicationSpecification<'Model,'Message>) =
        let state = Mutable.create applicationInfo.Model
        
        // Map our state directly into the view context - this gives us something that can be data bound
        let viewContext = 
            let source = Binding.createObservableSource<'Message>()        
            
            source.OutputObservables <| applicationInfo.Binding source (state :> _)
            source

        // Permanently subscribe to the observables, and call our update function
        // Note we're not allowing this to be a disposable subscription - we need to force it to
        // stay alive, even in Xamarin Forms where the "render" method doesn't do the final rendering
        viewContext.Add (fun msg -> state.Value <- applicationInfo.Update msg state.Value)
        
        // Render the "application"
        applicationInfo.Render viewContext
        

