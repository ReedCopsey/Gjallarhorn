namespace Gjallarhorn

open System.Threading
open System.Windows.Threading

/// Platform installation
module Wpf =
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
        let targetType = typedefof<Gjallarhorn.Bindable.Wpf.DesktopBindingTarget<_>>.MakeGenericType([|typ|])
        System.Activator.CreateInstance(targetType) 

    /// Installs WPF targets for binding into Gjallarhorn
    let install installSynchronizationContext =        
        Gjallarhorn.Bindable.Binding.Implementation.installCreationFunction (fun _ -> creation typeof<obj>) creation

        match installSynchronizationContext with
        | true -> installAndGetSynchronizationContext ()
        | false -> SynchronizationContext.Current
