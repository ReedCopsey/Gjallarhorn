namespace Gjallarhorn.Bindable

open System
open Gjallarhorn
open Gjallarhorn.Bindable

module Framework =
    
    type ApplicationCore<'Model,'Message> = 
        {
            Model : ISignal<'Model> 
            Init : unit -> unit // Initialization function which runs once after platforms are installed
            Update : 'Message -> unit
            Binding : ObservableBindingSource<'Message> -> ISignal<'Model> -> IObservable<'Message> list
        }

    type CreateDataContext<'Message> = System.Threading.SynchronizationContext -> ObservableBindingSource<'Message>

    let application model init update binding = { Model = model ; Init = init ; Update = update ; Binding = binding }
    let basicApplication model update binding = 
        let m = Mutable.create model
        let upd msg = m.Value <- update msg m.Value
            
        { Model = m :> ISignal<_>; Init = ignore ; Update = upd ; Binding = binding }

    type ApplicationSpecification<'Model,'Message> = 
        { 
            Core : ApplicationCore<'Model,'Message>
            Render : CreateDataContext<'Message> -> int
        }
        with 
            member this.Model = this.Core.Model
            member this.Update = this.Core.Update
            member this.Binding = this.Core.Binding   
                
    let runApplication<'Model,'Message> (applicationInfo : ApplicationSpecification<'Model,'Message>) =        
        // Map our state directly into the view context - this gives us something that can be data bound
        let viewContext (ctx : System.Threading.SynchronizationContext) = 
            let source = Binding.createObservableSource<'Message>()        
            let model = 
                applicationInfo.Model 
                |> Signal.observeOn ctx

            source.OutputObservables <| applicationInfo.Binding source model

            // Permanently subscribe to the observables, and call our update function
            // Note we're not allowing this to be a disposable subscription - we need to force it to
            // stay alive, even in Xamarin Forms where the "render" method doesn't do the final rendering
            source.Add applicationInfo.Update
            source
        
        // Render the "application"
        applicationInfo.Render viewContext
           