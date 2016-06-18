(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.1/Facades/System.Runtime.dll"

(**
Mutables in Gjallarhorn
========================

One core type in Gjallarhorn is a Mutable. 

A Mutable extends the concept of a signal by allowing the current value to be changed.

The simplest Mutable can be created via `Mutable.create`:
*)

#r "Gjallarhorn.dll"
open Gjallarhorn

// Create a mutable variable
let m = Mutable.create 42

// Prints "Value = 42"
printfn "Value = %d" m.Value

m.Value <- 24
// Prints "Value = 24"
printfn "Value = %d" m.Value

(**

As it's name suggests, `Mutable.create` takes an initial value and wraps it into a Mutable, which is represented via the `IMutatable<'a>` interface.  

`IMutatable<'a>` inherits `ISignal<'a>`, but extends the `.Value` property to be both gettable and settable, which we can use to fetch or mutate the current value at any point.

As all Mutables are also signals, the `Signal` module functionality still works:

*)

// Create a mutable variable
let m = Mutable.create 0

Signal.Subscription.create (fun currentValue -> printfn "Value is now %d" currentValue) m 

// After this is set, a print will occur
// Prints: "Value is now 1"
m.Value <- 1

// After this is set, a second print will occur with the new value
// Prints: "Value is now 2"
m.Value <- 2

(**
 
Now, let's move on to [Validation in Gjallarhorn](validation.html).    
*)