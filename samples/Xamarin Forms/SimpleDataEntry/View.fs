namespace SimpleDataEntry

open Xamarin.Forms
open Xamarin.Forms.Xaml

open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Validation

type Page1() as this = 
    inherit Xamarin.Forms.ContentPage()
    
    do
        this.LoadFromXaml(typeof<Page1>) |> ignore

module VM =
    let createTop () = 
        let bt = Binding.createSource()        
        
        // Show our current value
        let currentValue = Mutable.create 0        
        let result = bt.ToFromView(currentValue, "Current", Validators.lessThan 10) 

        bt.CommandFromView "Increment"
        |> Observable.subscribe (fun _ -> currentValue.Value <- currentValue.Value + 1)
        |> bt.AddDisposable

        bt

    let createBottom () = 
        let bind = Binding.createSource()        
        
        // Show our current value
        let currentValue = Mutable.create 91
                        
        bind.MutateToFromView (
                    currentValue, 
                    "Current", 
                    string, 
                    Converters.stringToInt32 >> Validators.greaterThan 90 >> Validators.lessOrEqualTo 95)

        bind.ToView(currentValue, "CurrentValue")

        bind

type App() as self =
    inherit Application()

    let page = Page1()
    let top = page.FindByName<Grid>("Top") 
    let bottom = page.FindByName<Grid>("Bottom") 
    do 
        top.BindingContext <- VM.createTop()
        bottom.BindingContext <- VM.createBottom()
        self.MainPage <- page

    override __.OnStart() = ()
    override __.OnSleep() = ()
    override __.OnResume() = ()
