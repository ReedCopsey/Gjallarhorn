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

module Framework =
    open Gjallarhorn
    open Gjallarhorn.Bindable
    open System
    open System.Windows

    type WpfApplicationInfo<'Model,'Message> = 
        { 
            Core : Framework.ApplicationCore<'Model, 'Message>
            View : Window
        }
        with
            member this.ToApplicationSpecification render : Framework.ApplicationSpecification<'Model,'Message> = 
                { Core = { Model = this.Core.Model ; Update = this.Core.Update ; Binding = this.Core.Binding } ; Render = render }            

    let fromInfoAndWindow core window = { Core = core ; View = window }

    let runApplication<'Model,'Message> (applicationInfo : WpfApplicationInfo<'Model,'Message>) =
        let render dataContext = 
            applicationInfo.View.DataContext <- dataContext
            Application().Run(applicationInfo.View)

        Platform.install true |> ignore
        Gjallarhorn.Bindable.Framework.application (applicationInfo.ToApplicationSpecification render) 