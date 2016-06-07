namespace SimpleDataEntry

open Xamarin.Forms
open Xamarin.Forms.Xaml

open Gjallarhorn
open Gjallarhorn.Bindable

type Page1() as this = 
    inherit Xamarin.Forms.ContentPage()
    
    do
        this.LoadFromXaml(typeof<Page1>) |> ignore

module VM =
    let createTop () = 
        let bt = Binding.createTarget()        
        
        // Show our current value
        let currentValue = Mutable.create 0        
        bt.Watch "Current" currentValue

        let incr = bt.Command "Increment"
        incr.Subscribe(fun _ -> currentValue.Value <- currentValue.Value + 1)
        |> bt.AddDisposable

        bt

    let createBottom () = 
        let bt = Binding.createTarget()        
        
        // Show our current value
        let currentValue = Mutable.create 100        
        bt.Watch "Current" currentValue 

        bt.Command "Decrement"
        |> Observable.subscribe(fun _ -> currentValue.Value <- currentValue.Value - 1)
        |> bt.AddDisposable

        bt

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
