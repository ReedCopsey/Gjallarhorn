namespace Gjallarhorn

open System
open System.Threading

/// Platform installation
module XamarinForms =
    let private creation (typ : System.Type) =
        let targetType = typedefof<Gjallarhorn.Bindable.Xamarin.RefTypeBindingTarget<_>>.MakeGenericType([|typ|])
        System.Activator.CreateInstance(targetType) 

    /// Installs Xamarin Forms targets for binding into Gjallarhorn
    [<CompiledName("Install")>]
    let install () =        
        Gjallarhorn.Bindable.Binding.Implementation.installCreationFunction (fun _ -> creation typeof<obj>) creation
