﻿namespace Amazon.SimpleWorkflow.Extensions

open System
open System.Threading

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Model

open SWF.Extensions.Core.Model

/// Encapsulates the work a decision worker performs (i.e. take a decision task and make some decisions).
/// This class handles the boilerplate of: 
///     polling for tasks
///     responding with decisions
///     handling exceptions
type DecisionWorker private (
                                clt          : AmazonSimpleWorkflowClient,
                                domain       : string,
                                tasklist     : string,                             
                                decide       : DecisionTask -> Decision[] * string, // function that makes the decisions based on task, and a new execution context
                                onExn        : Exception -> unit,                   // function that handles exceptions
                                ?concurrency : int                                  // the number of concurrent workers
                             ) = 
    let concurrency = defaultArg concurrency 1

    let handler = async {
        while true do
            try
                let pollReq = PollForDecisionTaskRequest(Domain = domain, TaskList = TaskList(Name = tasklist), ReverseOrder = true)
                let! pollRes = clt.PollForDecisionTaskAsync(pollReq)
                let task = DecisionTask(pollRes.PollForDecisionTaskResult.DecisionTask)

                let decisions, execContext = decide(task) |> (fun (decisions, cxt) -> decisions |> Array.map (fun x -> x.ToSwfDecision()), cxt)
                let req = RespondDecisionTaskCompletedRequest()
                            .WithTaskToken(task.TaskToken)
                            .WithDecisions(decisions)
                            .WithExecutionContext(execContext)
                do! clt.RespondDecisionTaskCompletedAsync(req) |> Async.Ignore
            with exn -> 
                // invoke the supplied exception handler
                onExn(exn)                
    }

    do seq { 1..concurrency } |> Seq.iter (fun _ -> Async.Start handler)
    
    /// Starts a decision worker with C# lambdas
    static member Start(clt             : AmazonSimpleWorkflowClient,
                        domain          : string,
                        tasklist        : string,                        
                        decide          : Func<DecisionTask, Decision[] * string>,
                        onExn           : Action<Exception>) =
        let decide, onExn = (fun t -> decide.Invoke(t)), (fun exn -> onExn.Invoke(exn))
        DecisionWorker(clt, domain, tasklist, decide, onExn) |> ignore
    
    /// Starts a decision worker with C# lambdas, and specifies the level of concurrency to use
    static member Start(clt             : AmazonSimpleWorkflowClient,
                        domain          : string,
                        tasklist        : string,                        
                        decide          : Func<DecisionTask, Decision[] * string>,
                        onExn           : Action<Exception>,
                        concurrency     : int) =
        let decide, onExn = (fun t -> decide.Invoke(t)), (fun exn -> onExn.Invoke(exn))
        DecisionWorker(clt, domain, tasklist, decide, onExn, concurrency) |> ignore

    /// Starts a decision worker with F# functions, optionally specifying the level of concurrency to use
    static member Start(clt             : AmazonSimpleWorkflowClient,
                        domain          : string,
                        tasklist        : string,                        
                        decide          : DecisionTask -> Decision[] * string,
                        onExn           : Exception -> unit,
                        ?concurrency    : int) =
        DecisionWorker(clt, domain, tasklist, decide, onExn, ?concurrency = concurrency) |> ignore

/// Encapsulates the work an activity worker performs (i.e. taking an activity task and doing something with it).
/// This class handles the boilerplate of:
///     polling for tasks
///     responding with heartbeat
///     responding with failure when exception occurs in handling code
///     responding with complete when handling code succeeds
///     handling other exceptions
type ActivityWorker private (
                                clt             : AmazonSimpleWorkflowClient,
                                domain          : string,
                                tasklist        : string,
                                work            : string -> string,            // function that performs the activity and returns its result
                                onExn           : Exception -> unit,           // function that handles exceptions
                                ?heartbeatFreq  : TimeSpan,                    // how frequently we should respond with a heartbeat
                                ?concurrency    : int                          // the number of concurrent workers                                
                            ) =
    let heartbeatFreq = defaultArg heartbeatFreq (TimeSpan.FromMinutes 1.0)
    let concurrency = defaultArg concurrency 1

    // function to poll for activity tasks to perform
    let pollTask () = async {
        let pollReq = PollForActivityTaskRequest(Domain = domain, TaskList = TaskList(Name = tasklist))
        let! pollRes = clt.PollForActivityTaskAsync(pollReq)
        return pollRes.PollForActivityTaskResult.ActivityTask
    }

    // function to record heartbeats periodically
    let recordHeartbeat (task : ActivityTask) = async {
        while true do
            do! Async.Sleep(int heartbeatFreq.TotalMilliseconds)
            let req = RecordActivityTaskHeartbeatRequest()
                        .WithTaskToken(task.TaskToken)
            do! clt.RecordActivityTaskHeartbeatAsync(req) |> Async.Ignore
    }

    // task handler function
    let getTaskHandler (task : ActivityTask) = 
        let cts = new CancellationTokenSource()

        let handler = async {
            try 
                let result = work(task.Input)
                let req = RespondActivityTaskCompletedRequest()
                            .WithTaskToken(task.TaskToken)
                            .WithResult(result)
                do! clt.RespondActivityTaskCompletedAsync(req) |> Async.Ignore
                cts.Dispose()
            with exn ->
                // include the exception's message and stacktrace in the response
                let req = RespondActivityTaskFailedRequest()
                            .WithTaskToken(task.TaskToken)
                            .WithReason(exn.Message)
                            .WithDetails(exn.StackTrace)                        
                do! clt.RespondActivityTaskFailedAsync(req) |> Async.Ignore
                cts.Dispose()
        }

        handler, cts

    let start = async {
        while true do
            try
                let! task = pollTask()
                let handler, cts = getTaskHandler(task)

                // start the heart beat in a separate async computation, but use the same 
                // cancellation token as the task handler
                Async.Start(recordHeartbeat(task), cts.Token)
                Async.StartImmediate(handler)
            with exn ->
                // invokes the specified exception handler to handle non-hanlder errors
                onExn(exn)
    }

    do seq { 1..concurrency } |> Seq.iter (fun _ -> Async.Start start)

    /// Starts an activity worker with C# lambdas
    static member Start(clt             : AmazonSimpleWorkflowClient,
                        domain          : string,
                        tasklist        : string,                        
                        work            : Func<string, string>,
                        onExn           : Action<Exception>) =
        let work, onExn = (fun t -> work.Invoke(t)), (fun exn -> onExn.Invoke(exn))
        ActivityWorker(clt, domain, tasklist, work, onExn) |> ignore

    /// Starts an activity worker with C# lambdas, and specifies the level of concurrency to use
    static member Start(clt             : AmazonSimpleWorkflowClient,
                        domain          : string,
                        tasklist        : string,                        
                        work            : Func<string, string>,
                        onExn           : Action<Exception>,
                        concurrency     : int) =
        let work, onExn = (fun t -> work.Invoke(t)), (fun exn -> onExn.Invoke(exn))
        ActivityWorker(clt, domain, tasklist, work, onExn, ?concurrency = Some concurrency) |> ignore

    /// Starts an activity worker with C# lambdas, and specifies the heart beat frequency to use
    static member Start(clt             : AmazonSimpleWorkflowClient,
                        domain          : string,
                        tasklist        : string,                        
                        work            : Func<string, string>,
                        onExn           : Action<Exception>,
                        heartbeatFreq   : TimeSpan) =
        let work, onExn = (fun t -> work.Invoke(t)), (fun exn -> onExn.Invoke(exn))
        ActivityWorker(clt, domain, tasklist, work, onExn, ?heartbeatFreq = Some heartbeatFreq) |> ignore

    /// Starts an activity worker with C# lambdas, and specifies the heart beat frequency and level 
    /// of concurrency to use
    static member Start(clt             : AmazonSimpleWorkflowClient,
                        domain          : string,
                        tasklist        : string,                        
                        work            : Func<string, string>,
                        onExn           : Action<Exception>,
                        heartbeatFreq   : TimeSpan,
                        concurrency     : int) =
        let work, onExn = (fun t -> work.Invoke(t)), (fun exn -> onExn.Invoke(exn))
        ActivityWorker(clt, domain, tasklist, work, onExn, heartbeatFreq, concurrency) |> ignore

    /// Starts an activity worker with F# functions
    static member Start(clt             : AmazonSimpleWorkflowClient,
                        domain          : string,
                        tasklist        : string,                        
                        work            : string -> string,
                        onExn           : Exception -> unit,
                        ?heartbeatFreq  : TimeSpan,
                        ?concurrency    : int) =
        ActivityWorker(clt, domain, tasklist, work, onExn, ?heartbeatFreq = heartbeatFreq, ?concurrency = concurrency) |> ignore