// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

open System

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Model

open SWF.Extensions.Core.Model
open SWF.Extensions.Core.Workflow

let client = new AmazonSimpleWorkflowClient()

let decide (task : DecisionTask) = 
    //[| CompleteWorkflowExecution(Some "All done") |]
    seq { 1..10 }
    |> Seq.map (fun n -> ScheduleActivityTask(sprintf "%d" n, ActivityType(Name = "TransformPlayerData", Version = "5"), None, None, None, None, None, None, None))
    |> Seq.toArray

let action (task : ActivityTask) = sprintf "%s done" task.ActivityId

let onExn (exn : Exception) = 
    printf "%A" exn

[<EntryPoint>]
let main argv = 
    let workflow = 
        Workflow(domain = "iwi", name = "hello_world", description = "test workflow", version = "1")
        ++> Activity("greet", "say hi", (fun _ -> printf "hello"; "my name is Yan"),
                     60<sec>, 10<sec>, 10<sec>, 20<sec>)
        ++> Activity("echo", "echo", (fun str -> printf "%s" str; ""),
                     60<sec>, 10<sec>, 10<sec>, 20<sec>)
        
    workflow.Start(client)

    Console.ReadKey() |> ignore
    0