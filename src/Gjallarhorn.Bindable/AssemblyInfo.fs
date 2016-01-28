namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Gjallarhorn.Bindable")>]
[<assembly: AssemblyProductAttribute("Gjallarhorn")>]
[<assembly: AssemblyDescriptionAttribute("Framework for managing mutable data with signaling")>]
[<assembly: AssemblyVersionAttribute("0.0.1")>]
[<assembly: AssemblyFileVersionAttribute("0.0.1")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.1"
