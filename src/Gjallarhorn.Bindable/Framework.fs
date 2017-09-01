namespace Gjallarhorn.Bindable.Framework

open Gjallarhorn
open Gjallarhorn.Bindable

/// The core information required for an application
type ApplicationCore<'Model,'Message> (model, init, update, binding) =         

    // new (model, update, binding) = ApplicationCore(model, ignore, update, binding)
        
    //new (model, update, binding : (BindingSource -> ISignal<'Model> -> IObservable<'Message> option) list) = 
    //    ApplicationCore(model, ignore, update, Component binding)

    /// The function which generates the model
    member __.Model : unit -> ISignal<'Model> = model
    /// Initialization function which runs once after platforms are installed
    member __.Init : unit -> unit = init
    /// The update function
    member __.Update : 'Message -> unit = update
    /// The function which binds the model to the view
    member __.Binding : Component<'Model,'Message> = binding

/// Alias for a function to create a data context
type CreateDataContext<'Message> = System.Threading.SynchronizationContext -> ObservableBindingSource<'Message>

/// Full specification required to run an application
type ApplicationSpecification<'Model,'Message> = 
    { 
        /// The application core
        Core : ApplicationCore<'Model,'Message>
        /// The platform specific render function
        Render : CreateDataContext<'Message> -> unit
    }
    with 
        /// The model generator function from the core application
        member this.Model = this.Core.Model
        /// The update function from the core application
        member this.Update = this.Core.Update
        /// The binding function from the core application
        member this.Binding = this.Core.Binding   

/// A platform neutral application framework
[<AbstractClass;Sealed>]
type Framework =
        
    /// Build an application given a model generator, initialization function, update function, and binding function
    static member application model init update binding = ApplicationCore(model, init, update, binding)
    /// Build a basic application which manages state internally, given a initial model state, update function, and binding function
    static member basicApplication<'Model,'Message> (model : 'Model) (update : 'Message -> 'Model -> 'Model) binding = 
        let m = Mutable.createAsync model
        
        let upd msg = m.Update (update msg) |> ignore
            
        ApplicationCore((fun () -> m :> ISignal<_>), ignore, upd, Component.FromBindings binding)

    static member basicApplication2<'Model,'Message> (model : 'Model) (update : 'Message -> 'Model -> 'Model) binding = 
        let m = Mutable.createAsync model
        
        let upd msg = m.Update (update msg) |> ignore
            
        ApplicationCore((fun () -> m :> ISignal<_>), ignore, upd, Component.FromObservables binding)
    
    /// Run an application given the full ApplicationSpecification            
    static member runApplication<'Model,'Message> (applicationInfo : ApplicationSpecification<'Model,'Message>) =        
        // Map our state directly into the view context - this gives us something that can be data bound
        let viewContext (ctx : System.Threading.SynchronizationContext) = 
            let source = Binding.createObservableSource<'Message>()                    
            let model = 
                applicationInfo.Model () 
                |> Signal.observeOn ctx

            applicationInfo.Binding.Setup (source :> BindingSource) model
            |> source.OutputObservables

            // Permanently subscribe to the observables, and call our update function
            // Note we're not allowing this to be a disposable subscription - we need to force it to
            // stay alive, even in Xamarin Forms where the "render" method doesn't do the final rendering
            source.Add applicationInfo.Update
            source
        
        // Render the "application"
        applicationInfo.Render viewContext
           