(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/Gjallarhorn"
#I "../../bin/Gjallarhorn.Bindable"
#I "../../bin/Gjallarhorn.Bindable.Wpf"
#r "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.1/Facades/System.Runtime.dll"

(**
Mutables in Gjallarhorn
========================

The other core type in Gjallarhorn is a Mutable. 

A Mutable extends the concept of a view by allowing the current value to be changed.

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

`IMutatable<'a>` inherits `IView<'a>`, but extends the `.Value` property to be both gettable and settable, which we can use to fetch or mutate the current value at any point.

As all Mutables are also Views, the `View` module functionality still works:

*)

// Create a mutable variable
let m = Mutable.create 0

View.subscribe (fun currentValue -> printfn "Value is now %d" currentValue) m 

// After this is set, a print will occur
// Prints: "Value is now 1"
m.Value <- 1

// After this is set, a second print will occur with the new value
// Prints: "Value is now 2"
m.Value <- 2

(**

Mutatables can also be transformed via a map operation.  Note that mapping operations require
two mapping functions - one to go from the original type to the new (for the View), and a second
to go from the new type back to the original (for the edit operation)

*)

// Create a value
let source = Mutable.create 0

// Create a map that transforms this into a string
let mapped = Mutable.map (fun v -> v.ToString()) (fun s -> System.Int32.Parse(s)) source

// Subscribe to the final notifications:
View.subscribe (fun v -> printfn "Mapped value is %s" v) mapped

// Now, let's set some values:
// Prints: "Mapped value is 1"
source.Value <- 1

// Prints: "Mapped value is 3"
source.Value <- 3

// Now set via our mapping:
// Prints: "Mapped value is 42"
mapped.Value <- "42"

printfn "Source = %d, mapped = %s" source.Value mapped.Value


(**

Simple mappings between types where both implement IConvertable can be done directly:

*)

let mapped' = Mutable.mapConvertible<int,string> source

mapped'.Value <- "54"
printfn "Source = %d, mapped = %s, mapped' = %s" source.Value mapped.Value mapped'.Value

(**

The `Mutable` module also provides the option to map via a stepping function.  This is frequently useful
when mapping from a record to multiple editors:

*)

type Counts = { Value : int ; Name : string }

let count = Mutable.create { Value = 0 ; Name = "" }

let nameEditor = Mutable.step (fun c -> c.Name) (fun c n -> {c with Name = n}) count
let valueEditor = Mutable.step (fun c -> c.Value) (fun c v -> {c with Value = v}) count

nameEditor.Value <- "Reed"
valueEditor.Value <- 42

// Prints: "New count = Reed: 42
printfn "New count = %s: %d" count.Value.Name count.Value.Value
