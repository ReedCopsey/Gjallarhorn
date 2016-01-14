(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.1/Facades/System.Runtime.dll"

(**
A Brief Introduction to Gjallarhorn
========================

Gjallarhorn is centered around two core types - Views and Mutables.

A View represents a value that changes over time, and has the following properties:
- A current value, which can always be fetched
- A mechanism to signal changes to subscribers that the value has been updated

A Mutable is a holder for mutable variables, similar to a reference cell.  They are typically created via Mutable.create:

*)
#r "Gjallarhorn.dll"
open Gjallarhorn

// Create a mutable variable
let variable = Mutable.create "Foo"
printfn "hello = %s" variable.Value
variable.Value <- "Bar"
printfn "hello = %s" variable.Value

(**

In this simple example, we show creating, reading from, and updating a Mutable.

Mutable variables in Gjallarhorn have some distict features:

- Very low memory overhead: When created, a mutable like the one above has no memory overhead above that of a standard reference cell. It is effectively nothing but a thin wrapper around the current value, without any extra fields.
- They expose themselves as a View - there is always a current value and they can notify subscribers of changes

Once your core data is held within a Mutable or pulled in via something that provides a View, you can use the View module to transform the data

*)

// Create mutable variables
let first = Mutable.create ""
let last = Mutable.create ""
// Map these two variables from a first and last name to a full name
let full = View.map2 (fun f l -> f + " " + l) first last

printfn "initial = %s" full.Value
first.Value <- "Reed"
last.Value <- "Copsey"
// Prints: "Hello Reed Copsey!"
printfn "Hello %s!" full.Value

(**

We can also use an applicative functor to generate a view off any number of input views:

*)

let middle = Mutable.create ""
// Map these two variables from a first and last name to a full name using the view CE
let full' = View.pure' (fun f m l -> sprintf "%s %s %s" f m l) <*> first <*> middle <*> last

middle.Value <- "D."

// Also prints: "Hello Reed D. Copsey!"
printfn "Hello %s!" full'.Value

last.Value <- "Copsey, Jr."
// Now prints: "Hello Reed D. Copsey, Jr.!"
printfn "Hello %s!" full'.Value

(** 

Next, we'll go into [more details about Views](views.html).

*)