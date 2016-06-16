namespace SimpleDataEntry

open System

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
    // Create a source that reports values coming from an observable
    let createReportingSource (initialValue : int) (updateStream : IObservable<int>) = 
        let source = Binding.createSource()                        

        // Take our initial + observable and turn it into a signal
        let current = source.ObservableToSignal initialValue updateStream
        
        // Output it to the view
        source.ToView(current, "Current") 

        source

    let createEditSource initialValue validation = 
        let source = Binding.createObservableSource()        
        
        // Create a holder for current value
        let currentValue = Mutable.create initialValue

        // Bind the value to the view, and return a new signal 
        // of the validated input from user
        source.ToFromView (
                currentValue, 
                "Current", 
                string, // Convert to string on the fly 
                Converters.stringToInt32 >> validation) // Back to int (for Xamarin), then validate
        |> source.OutputObservable  // Pipe this as our output directly

        source

type App() as self =
    inherit Application()

    let page = Page1()
    let resultsSource = page.FindByName<Grid>("ResultsSource") 
    let editSource = page.FindByName<Grid>("EditSource") 

    // These are being set here, just to demonstrate "defining once" and passing through system
    let initialValue = 12
    let validation = Validators.greaterThan 10 >> Validators.lessOrEqualTo 25

    do 
        // Create our edit source
        // results implements IObservable<int>
        let results = VM.createEditSource initialValue validation
        
        // Set it as our binding context
        editSource.BindingContext <- results

        // Setup results binding context 
        resultsSource.BindingContext <- VM.createReportingSource initialValue results
        self.MainPage <- page

    override __.OnStart() = ()
    override __.OnSleep() = ()
    override __.OnResume() = ()
