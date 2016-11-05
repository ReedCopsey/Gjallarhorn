namespace Gjallarhorn.XamarinForms

open System
open System.Threading

/// Platform installation
module Platform =
    let private creation (typ : System.Type) =
        let sourceType = typedefof<Gjallarhorn.XamarinForms.RefTypeBindingTarget<_>>.MakeGenericType([|typ|])
        System.Activator.CreateInstance(sourceType) 

    /// Installs Xamarin Forms targets for binding into Gjallarhorn
    [<CompiledName("Install")>]
    let install () =        
        Gjallarhorn.Bindable.Binding.Implementation.installCreationFunction (fun _ -> creation typeof<obj>) creation

module Framework =
    open Gjallarhorn
    open Gjallarhorn.Bindable    

    open Xamarin.Forms

    type App(page) as self =
        inherit Application()    
        do         
            self.MainPage <- page

    type XamarinApplicationInfo<'Model,'Message> = 
        { 
            Core : Framework.ApplicationCore<'Model, 'Message>
            View : Page
        }
        with
            member this.ToApplicationSpecification render : Framework.ApplicationSpecification<'Model,'Message> = 
                { Core = { Model = this.Core.Model ; Update = this.Core.Update ; Binding = this.Core.Binding } ; Render = render }            

            member this.CreateApp() =
                let render dataContext = 
                    this.View.BindingContext <- dataContext     
                    1       
                Platform.install ()
                Gjallarhorn.Bindable.Framework.application (this.ToApplicationSpecification render) |> ignore
                App(this.View)                

    [<CompiledName("CreateApplicationInfo")>]
    let createApplicationInfo core view = { Core = core ; View = view }
