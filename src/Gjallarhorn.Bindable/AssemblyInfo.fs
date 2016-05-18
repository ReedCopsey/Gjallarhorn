namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Gjallarhorn.Bindable")>]
[<assembly: AssemblyProductAttribute("Gjallarhorn")>]
[<assembly: AssemblyDescriptionAttribute("Framework for managing mutable data with change notification and live views")>]
[<assembly: AssemblyVersionAttribute("0.0.2")>]
[<assembly: AssemblyFileVersionAttribute("0.0.2")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.2"
