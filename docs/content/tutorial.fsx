(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"

(**
Introducing Gjallarhorn
========================

Gjallarhorn is centered around two core types - Views and Mutables.

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

*)
