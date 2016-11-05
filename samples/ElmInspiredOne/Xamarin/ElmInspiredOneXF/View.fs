namespace ElmInspiredXF

open Xamarin.Forms
open Xamarin.Forms.Xaml

open Gjallarhorn.Bindable

open ElmInspiredOne

type MainPage() as this = 
    inherit Xamarin.Forms.ContentPage()    
    do
        this.LoadFromXaml(typeof<MainPage>) |> ignore
