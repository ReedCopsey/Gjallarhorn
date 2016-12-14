namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Gjallarhorn.Bindable.Wpf")>]
[<assembly: AssemblyProductAttribute("Gjallarhorn")>]
[<assembly: AssemblyDescriptionAttribute("Framework for managing mutable data with change notification and live views")>]
[<assembly: AssemblyVersionAttribute("0.0.8")>]
[<assembly: AssemblyFileVersionAttribute("0.0.8")>]
[<assembly: AssemblyCopyrightAttribute("Copyright 2016 Reed Copsey, Jr.")>]
[<assembly: AssemblyCompanyAttribute("Reed Copsey, Jr.")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.8"
