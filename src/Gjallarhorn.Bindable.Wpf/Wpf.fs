namespace Gjallarhorn.Wpf

open System
open System.Threading
open System.Windows
open System.Windows.Threading
open Gjallarhorn
open Gjallarhorn.Bindable

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
        Gjallarhorn.Bindable.Bind.Implementation.installCreationFunction (fun _ -> creation typeof<obj>) creation

        match installSynchronizationContext with
        | true -> installAndGetSynchronizationContext ()
        | false -> SynchronizationContext.Current

module App =                    
    let toApplicationSpecification render (appCore : Framework.ApplicationCore<'Model, 'Message>) : Framework.ApplicationSpecification<'Model,'Message> = 
            { 
                Core = appCore
                Render = render 
            }                

/// WPF Specific implementation of the Application Framework
[<AbstractClass;Sealed>]
type Framework =
    /// Run an application given an Application generator, Window generator, and other required information
    static member RunApplication<'Model,'Message,'Application,'Window when 'Application :> Application and 'Window :> Window> (applicationCreation : unit -> 'Application, windowCreation : unit -> 'Window, applicationInfo : Framework.ApplicationCore<'Model,'Message>) =
        let render (createCtx : SynchronizationContext -> ObservableBindingSource<'Message>) = 
            let dataContext = createCtx SynchronizationContext.Current

            // Construct application first, which guarantees application resources are available
            let app = applicationCreation()
            // Construct main window and set data context
            let win = windowCreation()
            win.DataContext <- dataContext               
            
            // Use standdard WPF message pump
            app.Run win |> ignore

        Platform.install true |> ignore
        applicationInfo.Init ()
        Gjallarhorn.Bindable.Framework.Framework.runApplication (App.toApplicationSpecification render applicationInfo) 
    
    /// Run an application using Application.Current and a function to construct the main window
    static member RunApplication<'Model,'Message,'Window when 'Window :> Window> (windowCreation : System.Func<'Window>, applicationInfo : Framework.ApplicationCore<'Model,'Message>) =
        let render (createCtx : SynchronizationContext -> ObservableBindingSource<'Message>) = 
            let dataContext = createCtx SynchronizationContext.Current

            // Get or create the application first, which guarantees application resources are available
            // If we create the application, we assume we need to run it explicitly
            let app, run = 
                match Application.Current with
                | null -> Application(), true
                | a -> a, false

            // Use the main Window as our entry window
            let win = windowCreation.Invoke ()
            app.MainWindow <- win
            win.DataContext <- dataContext               

            // Use standdard WPF message pump
            if run then
                app.Run win |> ignore
            else                
                win.Show()                

        Platform.install true |> ignore
        applicationInfo.Init ()
        Gjallarhorn.Bindable.Framework.Framework.runApplication (App.toApplicationSpecification render applicationInfo) 