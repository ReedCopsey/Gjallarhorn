module Context

open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Validation

type Values = { One : int ; Two : int }


let create initialValue =
    
    // Create our binding subject
    let subject =  Binding.createSubject ()

    // Create two mutable values from our initial value
    let one = Mutable.create initialValue.One
    let two = Mutable.create initialValue.Two

    // Bind these directly (two-way)
    Binding.editDirect subject "Value1" noValidation one
    Binding.editDirect subject "Value2" noValidation two

    // Create a signal for our result
    let result = Signal.map2 (+) one two
    
    // Display the results
    subject.Watch "Result" result

    // Set the results as our output (optional)
    // Since we're doing this, the context acts as an observable stream of results
    // If we used Binding.createTarget instead of createSubject, we would skip this 
    // and not have an observable output
    subject.OutputObservable result

    // Return the binding subject
    subject