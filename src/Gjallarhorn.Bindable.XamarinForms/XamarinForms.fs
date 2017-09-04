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
        Gjallarhorn.Bindable.Bind.Implementation.installCreationFunction (fun _ -> creation typeof<obj>) creation

/// Xamarin Forms implementation of the basic application framework
module Framework =
    open Gjallarhorn
    open Gjallarhorn.Bindable    

    open Xamarin.Forms

    /// Default Xamarin Forms Application implementation
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
                { Core = Framework.ApplicationCore(this.Core.Model, this.Core.Init, this.Core.Update, this.Core.Binding) ; Render = render }            

            member this.CreateApp() =
                let render (createCtx : SynchronizationContext -> ObservableBindingSource<'Message>) = 
                    this.View.BindingContext <- createCtx SynchronizationContext.Current
                           
                Platform.install ()
                this.Core.Init ()
                Gjallarhorn.Bindable.Framework.Framework.runApplication (this.ToApplicationSpecification render) |> ignore
                App(this.View)                

    [<CompiledName("CreateApplicationInfo")>]
    /// Create the application core given a specific view
    let createApplicationInfo core view = { Core = core ; View = view }
