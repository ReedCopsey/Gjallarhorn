namespace SimpleDataEntry

open Xamarin.Forms
open Xamarin.Forms.Xaml

open Gjallarhorn
open Gjallarhorn.Validation

type Editor() as this =
    inherit Xamarin.Forms.Frame()

    do
        this.LoadFromXaml(typeof<Editor>) |> ignore

type Reporter() as this =
    inherit Xamarin.Forms.Frame()

    do
        this.LoadFromXaml(typeof<Reporter>) |> ignore

type Page1() as this = 
    inherit Xamarin.Forms.ContentPage()
    
    do
        this.LoadFromXaml(typeof<Page1>) |> ignore

type App() as self =
    inherit Application()    

    // These are being set here, just to demonstrate "defining once" and passing through system
    let initialValue = 12
    
    // Change validation rules here if desired...
    let validation = Validators.greaterThan 10 >> Validators.lessOrEqualTo 25

    // Create our context
    let context = BindingContexts.createCompositeSource validation initialValue
    
    // Create our content page
    let page = Page1(BindingContext = context)

    do         
        self.MainPage <- page
