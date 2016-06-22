namespace SimpleDataEntry

open System

open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Validation

module BindingContexts =
    // Create a source that reports values coming from an observable
    let createReportingSource (initialValue : int) (updateStream : IObservable<int>) = 
        let source = Binding.createSource()                        

        // Take our initial + observable and turn it into a signal
        let current = Signal.fromObservable initialValue updateStream
        
        // Output it to the view
        Binding.toView source "Current" current

        source

    // Create an observable source for editing 
    let createEditSource initialValue validation = 
        let source = Binding.createObservableSource()        
        
        // Create a holder for current value
        let currentValue = Mutable.create initialValue

        // Bind the value to the view, and return a new signal 
        // of the validated input from user
        Binding.toFromViewConvertedValidated 
            source 
            "Current" 
            string 
            (Converters.stringToInt32 >> validation) // Back to int (for Xamarin), then validate
            currentValue                
        |> Observable.filterSome // Pass through valid options
        |> source.OutputObservable  // Pipe this as our output directly

        source

    // Create a "composite" with an editor and a reporter
    let createCompositeSource validation initialValue =
        let source = Binding.createSource()
        
        // Create our edit source
        // results implements IObservable<int>
        let results = createEditSource initialValue validation

        // Create our reporting target
        let reporting = createReportingSource initialValue results

        // Set it as our binding context
        source |> Binding.constantToView "EditSource" results
        
        // Setup property for results
        source |> Binding.constantToView "ResultsSource" reporting

        source