(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin/Gjallarhorn"
#I "../../bin/Gjallarhorn.Bindable"
#I "../../bin/Gjallarhorn.Bindable.Wpf"
#r "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.1/Facades/System.Runtime.dll"

(**
Views in Gjallarhorn
========================

One of the core types in Gjallarhorn is a View. 

A View represents a value that changes over time, and has the following properties:
- A current value, which can always be fetched
- A mechanism to signal changes to subscribers that the value has been updated

You can think if a View as a window into data that changes over time.  It's similar to an observable, except that it always has a current value.

The simplest View can be created via `View.constant`:
*)

#r "Gjallarhorn.dll"
open Gjallarhorn

// Create a view over a constant value
let v = View.constant 42

// Prints "Value = 42"
printfn "Value = %d" v.Value

(**

As it's name suggests, `View.constant` takes a constant value and binds it into a View, which is represented via the `IView<'a>` interface.  

`IView<'a>` provides a simple `.Value` property, which we can use to fetch the current value at any point.

All mutables in Gjallarhorn also implement `IView<'a>`, allowing mutables to be used as views as well.  The `View` module provides functionality
which allows you to subscribe to changes on views:

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

Views can also be filtered or transformed:

*)

// Create a mutable value
let source = Mutable.create 0

// Create a filter on this
let filtered = View.filter (fun v -> v < 5) source

// Transform the filtered result:
let final = View.map (fun v -> sprintf "Filtered value is %d" v) filtered

// Subscribe to the final notifications:
View.subscribe (fun v -> printfn "%s" v) final

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

Views can be combined by using `View.map2` or via custom operators:

*)

let a = Mutable.create ""
let b = Mutable.create ""

let v1 = View.map2 (fun v1 v2 -> v1 + v2) a b 

a.Value <- "Foo"
b.Value <- "Bar"

// Prints: "v1.Value = FooBar"
printfn "v1.Value = %s" v1.Value

// Use custom operators to combine many views at once, including mixed types
let c = Mutable.create ""
let d = Mutable.create 0

let v2 = (fun v1 v2 v3 v4 -> sprintf "%s%s%s : %d" v1 v2 v3 v4) <!> a <*> b <*> c <*> d

// Prints: "v2.Value = FooBar : 0"
printfn "v2.Value = %s" v2.Value

c.Value <- "Baz"
d.Value <- 42
// Prints: "v2.Value = FooBarBaz : 42"
printfn "v2.Value = %s" v2.Value

(**

Views are also closely related to observables.  The main difference between an `IView<'a>` and an `IObservable<'a>` is that the former has the 
notion of a value (represented in the `Value` property) which always exists and is current.

As these are so closely related, there are functions to convert to and from `IObservable<'a>` included in the `View` module.

*)

let e = Event<int>()
let observable = e.Publish

// Subscribe to the observable, with "0" as the initial value (since we always need a "current value")
let v = View.fromObservable 0 observable

// Prints: "View's value = 0"
printfn "View's value = %d" v.Value

e.Trigger 42

// Prints: "View's value = 42"
printfn "View's value = %d" v.Value

// Convert back to an observable
let obs = View.asObservable v

obs
|> Observable.add (fun v -> printfn "New value of observable = %d" v)

// Prints: "New value of observable = 54"
// Note that this starts as an observable, maps through a View, and back to an observable for the notification!
e.Trigger 54

(**
 
Now, let's move on to [Mutables in Gjallarhorn](mutables.html).    
*)