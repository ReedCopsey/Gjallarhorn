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
        let result = bt.Edit "Current" (Validation.Validators.lessThan 10) currentValue

        let incr = bt.Command "Increment"
        
        incr.Subscribe(fun _ -> currentValue.Value <- currentValue.Value + 1)
        |> bt.AddDisposable

        bt

    let createBottom () = 
        let bt = Binding.createTarget()        
        
        // Show our current value
        let currentValue = Mutable.create 100        
        let currentEdit = Signal.map string currentValue
        let valid =
            Validation.custom (fun a ->
                match System.Int32.TryParse a with
                | false, _ -> Some "Could not convert to number" 
                | true, v ->
                    if v > 95 then 
                        None 
                    else 
                        Some "Value must be >95" )            

        let out = bt.Edit "Current" valid currentEdit 
        out 
        |> Signal.Subscription.create (fun v -> 
            match System.Int32.TryParse v with
            | true, value -> currentValue.Value <- value
            | _ -> ())
        |> bt.AddDisposable
        

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
