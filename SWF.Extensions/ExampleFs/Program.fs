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
    "hi, this is the child workflow"

let bye name =
    printfn "good bye! %s" name
    ""

let childEcho str =
    printfn "%s" str
    "child workflow"

[<EntryPoint>]
let main argv = 
    let childWorkflow = 
        Workflow(domain = "iwi", name = "test_child_workflow", description = "test child workflow", version = "1",
                 execStartToCloseTimeout = 600, taskStartToCloseTimeout = 300,
                 childPolicy = ChildPolicy.Terminate)
        ++> Activity("child_echo", "child echos", childEcho, 60, 10, 10, 20)

    let workflow = 
        Workflow(domain = "iwi", name = "test_workflow", description = "test workflow", version = "1")
        ++> Activity("greet", "say hi", greet, 60, 10, 10, 20)
        ++> Activity("echo", "echo", echo, 60, 10, 10, 20)
        ++> childWorkflow
        ++> Activity("bye", "say bye", bye, 60, 10, 10, 20)
        
    workflow.Start(client)

    while true do
        System.Threading.Thread.Sleep(1000)
    0