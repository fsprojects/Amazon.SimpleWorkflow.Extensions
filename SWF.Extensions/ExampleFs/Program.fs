// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

open System

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Model

open SWF.Extensions.Core.Model
open SWF.Extensions.Core.Workflow

let client = new AmazonSimpleWorkflowClient()

let greet name =
    printfn "hello %s" name
    "my name is Yan"

let echo str = 
    printfn "%s" str
    ""

let bye _ =
    printfn "good bye!"
    ""

[<EntryPoint>]
let main argv = 
    let workflow = 
        Workflow(domain = "iwi", name = "test_workflow", description = "test workflow", version = "1")
        ++> Activity("greet", "say hi", greet, 60, 10, 10, 20)
        ++> Activity("echo", "echo", echo, 60, 10, 10, 20)
        ++> Activity("bye", "say bye", bye, 60, 10, 10, 20)
        
    workflow.Start(client)

    while true do
        System.Threading.Thread.Sleep(1000)
    0