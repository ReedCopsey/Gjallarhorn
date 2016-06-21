(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.1/Facades/System.Runtime.dll"

(**
Signals in Gjallarhorn
========================

One of the core types in Gjallarhorn is a Signal. 

A signal represents a value that changes over time, and has the following properties:
- A current value, which can always be fetched
- A mechanism to signal changes to subscribers that the value has been updated

You can think if a signal as a window into data that changes over time.  It's similar to an observable, and even implements IObservable for compatibility with other libraries, 
except that it always has a current value.

The simplest signal can be created via `Signal.constant`:
*)

#r "Gjallarhorn.dll"
open Gjallarhorn
open System

// Create a signal over a constant value
let s = Signal.constant 42

// Prints "Value = 42"
printfn "Value = %d" s.Value

(**

As it's name suggests, `Signal.constant` takes a constant value and binds it into a signal, which is represented via the `ISignal<'a>` interface.  

`ISignal<'a>` provides a simple `.Value` property, which we can use to fetch the current value at any point.

All mutables in Gjallarhorn also implement `ISignal<'a>`, allowing mutables to be used as signals as well.  The `Signal` module provides functionality
which allows you to subscribe to changes on signals:

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

Signals can also be filtered or transformed:

*)

// Create a mutable value
let source = Mutable.create 0

// Create a filter on this
let filtered = Signal.filter (fun s -> s < 5) source

// Transform the filtered result:
let final = Signal.map (fun s -> sprintf "Filtered value is %d" s) filtered

// Subscribe to the final notifications:
Signal.Subscription.create (fun s -> printfn "%s" s) final

// Now, let's set some values:
// Prints: "Filtered value is 1"
source.Value <- 1

// Prints: "Filtered value is 3"
source.Value <- 3

// Does not output anything, as the filter's predicate fails
source.Value <- 42

// Note that the filtered value retains its "last good value"
// Prints: "Source = 42, filtered = 3"
printfn "Source = %d, filtered = %d" source.Value filtered.Value

// Prints: "Filtered value is 2"
source.Value <- 2

(**

Signals can be combined by using `Signal.map2`, and even higher arity mapping functions:

*)

let a = Mutable.create ""
let b = Mutable.create ""

let v1 = Signal.map2 (fun v1 v2 -> v1 + v2) a b 

a.Value <- "Foo"
b.Value <- "Bar"

// Prints: "v1.Value = FooBar"
printfn "v1.Value = %s" v1.Value

// Mapping also works to combine many signals at once, including mixed types, up to 10 in an signal function
let c = Mutable.create ""
let d = Mutable.create 0

let v2 = Signal.map4 (fun v1 v2 v3 v4 -> sprintf "%s%s%s : %d" v1 v2 v3 v4) a b c d

// Prints: "v2.Value = FooBar : 0"
printfn "v2.Value = %s" v2.Value

c.Value <- "Baz"
d.Value <- 42
// Prints: "v2.Value = FooBarBaz : 42"
printfn "v2.Value = %s" v2.Value

(**

Signals are also closely related to observables.  The main difference between an `ISignal<'a>` and an `IObservable<'a>` is that the former has the 
notion of a value (represented in the `Value` property) which always exists and is current.

As these are so closely related, `ISignal<'a>` directly implements `IObservable<'a>`, and there is a function to convert from `IObservable<'a>` included in the `Signal` module.

*)

let e = Event<int>()
let observable = e.Publish

// Subscribe to the observable, with "0" as the initial value (since we always need a "current value")
let s = Signal.fromObservable 0 observable

// Prints: "Signal's value = 0"
printfn "Signal's value = %d" s.Value

e.Trigger 42

// Prints: "Signal's value = 42"
printfn "Signal's value = %d" s.Value

// Convert back to an observable
let obs = s :> IObservable<int>

obs
|> Observable.add (fun s -> printfn "New value of observable = %d" s)

// Prints: "New value of observable = 54"
// Note that this starts as an observable, maps through a Signal, and back to an observable for the notification!
e.Trigger 54

(**
 
Now, let's move on to [Mutables in Gjallarhorn](mutables.html).    
*)