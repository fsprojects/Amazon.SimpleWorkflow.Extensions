// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

open System

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Model

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
    DecisionWorker.Start(client, "iwi", "transformTaskList", decide, onExn)
    ActivityWorker.Start(client, "iwi", "transformPlayerTaskList", action, onExn)
    Console.ReadKey() |> ignore
    0