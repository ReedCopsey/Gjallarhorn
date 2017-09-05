(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use 
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"
#r "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.1/Facades/System.Runtime.dll"

(**
Gjallarhorn
===================

Gjallarhorn is a library designed to manage notifications and mutable state.  It provides mechanisms for signaling of changes, represented via signals.

Example
-------

This example demonstrates using basic functionality in Gjallarhorn.

*)
#r "Gjallarhorn.dll"
open Gjallarhorn

// Create a mutable variable
let var1 = Mutable.create 0
let var2 = Mutable.create 2
let result = Signal.map2 (fun a b -> a + b) var1 var2

Signal.Subscription.create (fun value -> printfn "The sum of our variables is %d" value) result

// Set first variable, which causes subscription to print
var1.Value <- 20
// Set first variable, which again causes subscription to print
var2.Value <- 22


(**

Core Types and Usage
-----------------------

 * [Introduction to Gjallarhorn](intro.html): A brief introduction to core concepts within Gjallarhorn.
 * [Signals](signals.html): Details about Signals
 * [Mutables](mutables.html): Details about Mutables
 * [Validation](validation.html): The core validation engine 
 * [Validating Signals](validate_types.html): Details about Validation of Signals

API Reference
-----------------------

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library. This includes additional brief samples on using most of the
   functions.

Gjallarhorn.Bindable
-----------------------

 * [Gjallarhorn.Bindable](/Gjallarhorn.Bindable) builds on top of this project to provide a unidirectional
   architecture for XAML platforms, including WPF and Xamarin Forms.
 
Contributing and copyright
--------------------------

The project is hosted on [GitHub](https://github.com/ReedCopsey/Gjallarhorn) where you can [report issues](https://github.com/ReedCopsey/Gjallarhorn/issues), fork 
the project and submit pull requests. 

The library is available under the MIT License, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file](https://github.com/ReedCopsey/Gjallarhorn/blob/master/LICENSE.txt) in the GitHub repository. 

*)
