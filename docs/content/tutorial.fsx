(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"

(**
Introducing your project
========================

Say more

*)
#r "Gjallarhorn.dll"
open Gjallarhorn

// Create a mutable variable
let variable = Mutable.create "Foo"
printfn "hello = %s" variable.Value

(**
TODO
*)
