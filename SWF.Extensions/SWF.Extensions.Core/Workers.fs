namespace Amazon.SimpleWorkflow.Extensions

open System
open System.Threading

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Model

open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Extensions.Model

open log4net

[<AutoOpen>]
module internal WorkerUtils = 
    let trimDetails = trim Constants.maxDetailsLength
    let trimReason  = trim Constants.maxReasonLength

exception ResultTooLong of int * string

/// Encapsulates the work a decision worker performs (i.e. take a decision task and make some decisions).
/// This class handles the boilerplate of: 
///     polling for tasks
///     responding with decisions
///     handling exceptions
type DecisionWorker private (
                                clt          : IAmazonSimpleWorkflow,
                                domain       : string,
                                tasklist     : string,
                                // function that makes the decisions based on task, and a new execution context
                                decide       : IAmazonSimpleWorkflow * DecisionTask -> Decision[] * string, 
                                onExn        : Exception -> unit,                   // function that handles exceptions
                                ?identity    : Identity,                            // identity of the worker (e.g. instance ID, IP, etc.)
                                ?concurrency : int                                  // the number of concurrent workers
                             ) = 
    let concurrency = defaultArg concurrency 1
    let logger      = LogManager.GetLogger(sprintf "DecisionWorker (Domain %s, TaskList %s, Concurrency %d)" domain tasklist concurrency)

    let pollForTask () = async {
        let req = PollForDecisionTaskRequest(Domain       = domain, 
                                             ReverseOrder = true,
                                             TaskList     = new TaskList(Name = tasklist))
        <@ req.Identity @> <-? identity

        let work = clt.PollForDecisionTaskAsync(req) |> Async.AwaitTask
        let! res = Async.WithRetry(work, 3)
        match res with
        | Choice1Of2 pollRes -> return Some pollRes
        | Choice2Of2 exn     -> onExn exn
                                return None
    }

    let respond token (decisions : Decision[]) execContext = async {
        let req = RespondDecisionTaskCompletedRequest(TaskToken        = token,
                                                      ExecutionContext = execContext)
        decisions 
        |> Seq.map (fun x -> x.ToSwfDecision())
        |> req.Decisions.AddRange

        let work = clt.RespondDecisionTaskCompletedAsync(req) |> Async.AwaitTask 
        let! res = Async.WithRetry(work, 3)
        match res with
        | Choice1Of2 _   -> return ()
        | Choice2Of2 exn -> onExn exn
                            return ()
    }

    let handler = async {
        while true do
            try
                let! pollRes = pollForTask()
                match pollRes with
                | Some pollRes when not <| nullOrWs pollRes.DecisionTask.TaskToken ->                    
                    let task = DecisionTask(pollRes.DecisionTask, clt, domain, tasklist)
                    let decisions, execContext = decide(clt, task) 
                    do! respond task.TaskToken decisions execContext
                | _ -> ()
            with exn -> 
                // invoke the supplied exception handler
                onExn(exn)
    }

    do seq { 1..concurrency } 
       |> Seq.iter (fun _ ->
            Async.StartWithContinuations (
                handler,
                (fun _      -> logger.Error("Async workflow has exited unexpectedly.")),
                (fun exn    -> logger.Error("Async workflow has exited with exception.", exn)),
                (fun canExn -> logger.Error("Async workflow has been cancelled.", canExn))))

    /// Starts a decision worker with C# lambdas with minimal set of inputs
    static member Start(clt             : IAmazonSimpleWorkflow,
                        domain          : string,
                        tasklist        : string,                        
                        decide          : Func<IAmazonSimpleWorkflow, DecisionTask, Decision[] * string>,
                        onExn           : Action<Exception>) =
        let decide, onExn = (fun t -> decide.Invoke(t)), (fun exn -> onExn.Invoke(exn))
        DecisionWorker(clt, domain, tasklist, decide, onExn) |> ignore
    
    /// Starts a decision worker with C# lambdas with greedy set of inputs
    static member Start(clt             : IAmazonSimpleWorkflow,
                        domain          : string,
                        tasklist        : string,                        
                        decide          : Func<IAmazonSimpleWorkflow, DecisionTask, Decision[] * string>,
                        onExn           : Action<Exception>,
                        identity        : Identity,
                        concurrency     : int) =
        let decide, onExn = (fun t -> decide.Invoke(t)), (fun exn -> onExn.Invoke(exn))
        DecisionWorker(clt, domain, tasklist, decide, onExn, identity, concurrency) |> ignore

    /// Starts a decision worker with F# functions, optionally specifying the level of concurrency to use
    static member Start(clt             : IAmazonSimpleWorkflow,
                        domain          : string,
                        tasklist        : string,                        
                        decide          : IAmazonSimpleWorkflow * DecisionTask -> Decision[] * string,
                        onExn           : Exception -> unit,
                        ?identity       : Identity,
                        ?concurrency    : int) =
        DecisionWorker(clt, domain, tasklist, decide, onExn, ?identity = identity, ?concurrency = concurrency) |> ignore

/// Encapsulates the work an activity worker performs (i.e. taking an activity task and doing something with it).
/// This class handles the boilerplate of:
///     polling for tasks
///     responding with heartbeat
///     responding with failure when exception occurs in handling code
///     responding with complete when handling code succeeds
///     handling other exceptions
type ActivityWorker private (
                                clt             : IAmazonSimpleWorkflow,
                                domain          : string,
                                tasklist        : string,
                                work            : string -> string,            // function that performs the activity and returns its result
                                onExn           : Exception -> unit,           // function that handles exceptions
                                ?heartbeatFreq  : TimeSpan,                    // how frequently we should respond with a heartbeat
                                ?identity       : Identity,                    // identity of the worker (e.g. instance ID, IP, etc.)                                
                                ?concurrency    : int                          // the number of concurrent workers                                
                            ) =
    let heartbeatFreq = defaultArg heartbeatFreq (TimeSpan.FromMinutes 1.0)
    let concurrency   = defaultArg concurrency 1
    let logger        = LogManager.GetLogger(sprintf "ActivityWorker (Domain %s, TaskList %s, Concurrency %d)" domain tasklist concurrency)

    // function to poll for activity tasks to perform
    let pollTask () = async {
        let req = PollForActivityTaskRequest(Domain = domain, TaskList = TaskList(Name = tasklist))
        <@ req.Identity @> <-? identity

        let work = clt.PollForActivityTaskAsync(req) |> Async.AwaitTask
        let! res = Async.WithRetry(work, 3)
        match res with
        | Choice1Of2 pollRes -> return Some pollRes.ActivityTask
        | Choice2Of2 exn     -> onExn exn
                                return None
    }

    // function to record heartbeats periodically
    let recordHeartbeat (task : ActivityTask) = async {
        while true do
            let req = RecordActivityTaskHeartbeatRequest(TaskToken = task.TaskToken)
            let work = clt.RecordActivityTaskHeartbeatAsync(req) |> Async.AwaitTask
            do! Async.WithRetry(work, 1) |> Async.Ignore
            do! Async.Sleep(int heartbeatFreq.TotalMilliseconds)
    }

    // task handler function
    let getTaskHandler (task : ActivityTask) = 
        let cts = new CancellationTokenSource()

        let respondFailure (exn : Exception) = async {
            let details = (str >> trimDetails) exn
            let reason  = trimReason exn.Message

            // include the exception's message and stacktrace in the response
            let req = RespondActivityTaskFailedRequest(TaskToken = task.TaskToken,
                                                       Reason    = reason,
                                                       Details   = details)
            let work = clt.RespondActivityTaskFailedAsync(req) |> Async.AwaitTask
            let! res = Async.WithRetry(work, 3)
            match res with
            | Choice2Of2 exn -> onExn(exn)
            | _              -> ()
        }

        let respondCompletion (result : string) =
            if result.Length > Constants.maxResultLength then
                respondFailure <| ResultTooLong(result.Length, result)
            else
                async {
                    let req = RespondActivityTaskCompletedRequest(TaskToken = task.TaskToken,
                                                                  Result    = result)
                    let work = clt.RespondActivityTaskCompletedAsync(req) |> Async.AwaitTask
                    let! res = Async.WithRetry(work, 3)
                    match res with
                    | Choice2Of2 exn -> onExn(exn)
                    | _              -> ()
                }

        let handler = async {
            try
                do! work(task.Input) |> respondCompletion
                cts.Cancel()
            with exn ->
                do! respondFailure exn                
                cts.Cancel()
        }

        handler, cts

    let start = async {
        while true do
            try
                let! pollRes = pollTask()

                match pollRes with
                | Some task when not <| nullOrWs task.TaskToken ->
                    let handler, cts = getTaskHandler(task)

                    // start the heart beat in a separate async computation, but use the same 
                    // cancellation token as the task handler
                    let heartbeat = Async.StartCatchCancellation(recordHeartbeat(task), cts.Token)
                    Async.Start(heartbeat)
                    Async.StartImmediate(handler)
                | _ -> ()
            with exn ->
                // invokes the specified exception handler to handle non-hanlder errors
                onExn(exn)
    }

    do seq { 1..concurrency } 
       |> Seq.iter (fun _ -> 
            Async.StartWithContinuations (
                start,
                (fun _      -> logger.Error("Async workflow has exited unexpectedly.")),
                (fun exn    -> logger.Error("Async workflow has exited with exception.", exn)),
                (fun canExn -> logger.Error("Async workflow has been cancelled.", canExn))))

    /// Starts an activity worker with C# lambdas with minimal set of inputs
    static member Start(clt             : IAmazonSimpleWorkflow,
                        domain          : string,
                        tasklist        : string,                        
                        work            : Func<string, string>,
                        onExn           : Action<Exception>) =
        let work, onExn = (fun t -> work.Invoke(t)), (fun exn -> onExn.Invoke(exn))
        ActivityWorker(clt, domain, tasklist, work, onExn) |> ignore

    /// Starts an activity worker with C# lambdas, and specifies the heart beat frequency to use
    static member Start(clt             : IAmazonSimpleWorkflow,
                        domain          : string,
                        tasklist        : string,                        
                        work            : Func<string, string>,
                        onExn           : Action<Exception>,
                        heartbeatFreq   : TimeSpan) =
        let work, onExn = (fun t -> work.Invoke(t)), (fun exn -> onExn.Invoke(exn))
        ActivityWorker(clt, domain, tasklist, work, onExn, ?heartbeatFreq = Some heartbeatFreq) |> ignore

    /// Starts an activity worker with C# lambdas with greedy set of inputs
    static member Start(clt             : IAmazonSimpleWorkflow,
                        domain          : string,
                        tasklist        : string,                        
                        work            : Func<string, string>,
                        onExn           : Action<Exception>,
                        heartbeatFreq   : TimeSpan,
                        identity        : Identity,                        
                        concurrency     : int) =
        let work, onExn = (fun t -> work.Invoke(t)), (fun exn -> onExn.Invoke(exn))
        ActivityWorker(clt, domain, tasklist, work, onExn, heartbeatFreq, identity, concurrency) |> ignore

    /// Starts an activity worker with F# functions
    static member Start(clt             : IAmazonSimpleWorkflow,
                        domain          : string,
                        tasklist        : string,                        
                        work            : string -> string,
                        onExn           : Exception -> unit,
                        ?heartbeatFreq  : TimeSpan,
                        ?identity       : Identity,                        
                        ?concurrency    : int) =
        ActivityWorker(clt, domain, tasklist, work, onExn, ?heartbeatFreq = heartbeatFreq, ?identity = identity, ?concurrency = concurrency) |> ignore