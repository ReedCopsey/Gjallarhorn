namespace Gjallarhorn.Wpf

open System.Threading
open System.Windows.Threading

/// Platform installation
module Platform =
    // Gets, and potentially installs, the WPF synchronization context
    let private installAndGetSynchronizationContext () =
        match SynchronizationContext.Current with
        | null ->
            // Create our UI sync context, and install it:
            DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)
            |> SynchronizationContext.SetSynchronizationContext
        | _ -> ()

        SynchronizationContext.Current

    let private creation (typ : System.Type) =
        let sourceType = typedefof<Gjallarhorn.Wpf.DesktopBindingSource<_>>.MakeGenericType([|typ|])
        System.Activator.CreateInstance(sourceType) 

    /// Installs WPF targets for binding into Gjallarhorn
    [<CompiledName("Install")>]
    let install installSynchronizationContext =        
        Gjallarhorn.Bindable.Binding.Implementation.installCreationFunction (fun _ -> creation typeof<obj>) creation

        match installSynchronizationContext with
        | true -> installAndGetSynchronizationContext ()
        | false -> SynchronizationContext.Current

/// WPF Specific implementation of the Application Framework
module Framework =
    open Gjallarhorn
    open Gjallarhorn.Bindable
    open System
    open System.Windows

    module App =                    
       let toApplicationSpecification render (appCore : Framework.ApplicationCore<'Model, 'Message>) : Framework.ApplicationSpecification<'Model,'Message> = 
                { 
                    Core = appCore
                    Render = render 
                }                
    /// Run an application given an Application generator, Window generator, and other required information
    let runApplication<'Model,'Message,'Application,'Window when 'Application :> Application and 'Window :> Window> (application : unit -> 'Application) (window : unit -> 'Window) (applicationInfo : Framework.ApplicationCore<'Model,'Message>) =
        let render (createCtx : SynchronizationContext -> ObservableBindingSource<'Message>) = 
            let dataContext = createCtx SynchronizationContext.Current

            let win = window()
            win.DataContext <- dataContext                        
            application().Run win

        Platform.install true |> ignore
        applicationInfo.Init ()
        Gjallarhorn.Bindable.Framework.runApplication (App.toApplicationSpecification render applicationInfo) 