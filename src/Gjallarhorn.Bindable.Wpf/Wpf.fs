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

    type ApplicationInfo<'Model,'Message> = 
        { 
            Model : 'Model 
            Update : 'Message -> 'Model -> 'Model
            Binding : ObservableBindingSource<'Message> -> ISignal<'Model> -> IObservable<'Message> list
            View : Window
        }

    let application<'Model,'Message> (applicationInfo : ApplicationInfo<'Model,'Message>) =
        let view' dataContext = 
            applicationInfo.View.DataContext <- dataContext
            Application().Run(applicationInfo.View)
        Gjallarhorn.Bindable.CoreFramework.application applicationInfo.Model applicationInfo.Update applicationInfo.Binding view'