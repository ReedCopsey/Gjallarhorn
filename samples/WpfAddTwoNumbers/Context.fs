module Context

open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Validation
open Gjallarhorn.Validation.Validators

let create () =    
    // Create our binding source
    let source = Binding.createSource ()

    // Create two mutable values from our initial values
    let one = Mutable.create 0
    let two = Mutable.create 0

    // Bind these directly (two-way)
    Binding.mutateToFromView source "Value1" one
    Binding.mutateToFromView source "Value2" two

    // Create a signal for our result
    let result = Signal.map2 (+) one two
    
    // Display the results bound as "Result"
    Binding.toView source "Result" result

    // Return the binding subject
    source