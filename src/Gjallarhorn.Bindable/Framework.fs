namespace Gjallarhorn.Bindable

open System
open Gjallarhorn
open Gjallarhorn.Bindable

/// A platform neutral application framework
module Framework =
        
    /// The core information required for an application
    type ApplicationCore<'Model,'Message> = 
        {
            /// The function which generates the model
            Model : unit -> ISignal<'Model> 
            /// Initialization function which runs once after platforms are installed
            Init : unit -> unit 
            /// The update function
            Update : 'Message -> unit
            /// The function which binds the model to the view
            Binding : Component<'Model,'Message>
        }

    /// Alias for a function to create a data context
    type CreateDataContext<'Message> = System.Threading.SynchronizationContext -> ObservableBindingSource<'Message>

    /// Build an application given a model generator, initialization function, update function, and binding function
    let application model init update binding = { Model = model ; Init = init ; Update = update ; Binding = binding }
    /// Build a basic application which manages state internally, given a initial model state, update function, and binding function
    let basicApplication model update binding = 
        let m = Mutable.create model
        let upd msg = m.Value <- update msg m.Value
            
        { Model = (fun _ -> m :> ISignal<_>) ; Init = ignore ; Update = upd ; Binding = binding }

    /// Full specification required to run an application
    type ApplicationSpecification<'Model,'Message> = 
        { 
            /// The application core
            Core : ApplicationCore<'Model,'Message>
            /// The platform specific render function
            Render : CreateDataContext<'Message> -> int
        }
        with 
            /// The model generator function from the core application
            member this.Model = this.Core.Model
            /// The update function from the core application
            member this.Update = this.Core.Update
            /// The binding function from the core application
            member this.Binding = this.Core.Binding   
    
    /// Run an application given the full ApplicationSpecification            
    let runApplication<'Model,'Message> (applicationInfo : ApplicationSpecification<'Model,'Message>) =        
        // Map our state directly into the view context - this gives us something that can be data bound
        let viewContext (ctx : System.Threading.SynchronizationContext) = 
            let source = Binding.createObservableSource<'Message>()                    
            let model = 
                applicationInfo.Model () 
                |> Signal.observeOn ctx

            applicationInfo.Binding (source :> _) model
            |> source.OutputObservables

            // Permanently subscribe to the observables, and call our update function
            // Note we're not allowing this to be a disposable subscription - we need to force it to
            // stay alive, even in Xamarin Forms where the "render" method doesn't do the final rendering
            source.Add applicationInfo.Update
            source
        
        // Render the "application"
        applicationInfo.Render viewContext
           