module Context

open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Validation

let create () =    
    // Create our binding target
    let target = Binding.createTarget ()

    // Create two mutable values from our initial values
    let one = Mutable.create 0
    let two = Mutable.create 0

    // Bind these directly (two-way)
    Binding.editDirect target "Value1" noValidation one
    Binding.editDirect target "Value2" noValidation two

    // Create a signal for our result
    let result = Signal.map2 (+) one two
    
    // Display the results bound as "Result"
    target.Watch "Result" result

    // Return the binding subject
    target