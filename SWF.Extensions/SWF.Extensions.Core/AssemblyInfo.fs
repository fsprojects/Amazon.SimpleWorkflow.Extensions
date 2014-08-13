namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Amazon.SimpleWorkflow.Extensions")>]
[<assembly: AssemblyProductAttribute("Amazon.SimpleWorkflow.Extensions")>]
[<assembly: AssemblyDescriptionAttribute("Provides an intuitive API for modelling workflows against SWF")>]
[<assembly: AssemblyVersionAttribute("1.2.1")>]
[<assembly: AssemblyFileVersionAttribute("1.2.1")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.2.1"
