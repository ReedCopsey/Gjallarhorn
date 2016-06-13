module Context

open Gjallarhorn
open Gjallarhorn.Bindable
open Gjallarhorn.Validation
open Gjallarhorn.Validation.Validators

let create () =    
    // Create our binding target
    let target = Binding.createTarget ()

    // Create two mutable values from our initial values
    let one = Mutable.create 0
    let two = Mutable.create 0

    // Bind these directly (two-way)
    Binding.mutateToFromView target "Value1" one
    Binding.mutateToFromView target "Value2" two

    // Create a signal for our result
    let result = Signal.map2 (+) one two
    
    // Display the results bound as "Result"
    target.ToView (result, "Result")

    // Return the binding subject
    target