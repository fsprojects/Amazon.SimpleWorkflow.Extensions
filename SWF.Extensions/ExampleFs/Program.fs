// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

open System

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Model

open SWF.Extensions.Core.Model
open SWF.Extensions.Core.Workflow

let client = new AmazonSimpleWorkflowClient()

let greet _ =
    printf "hello"
    "my name is Yan"

let echo str = 
    printf "%s" str
    ""

[<EntryPoint>]
let main argv = 
    let workflow = 
        Workflow(domain = "iwi", name = "hello_world", description = "test workflow", version = "1")
        ++> Activity("greet", "say hi", greet,
                     60<sec>, 10<sec>, 10<sec>, 20<sec>)
        ++> Activity("echo", "echo", echo,
                     60<sec>, 10<sec>, 10<sec>, 20<sec>)
        
    workflow.Start(client)

    while true do
        Console.ReadKey() |> ignore
    0