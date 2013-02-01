module SWF.Extensions.Core.Workflow

open System

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Model
open ServiceStack.Text

open SWF.Extensions.Core.Model
    
[<AutoOpen>]
module Helper =
    let nullOrWs = String.IsNullOrWhiteSpace

[<RequireQualifiedAccess>]
module HistoryEvents =
    /// Returns the input to the workflow from the list of events
    let rec getWorkflowInput = function
        | []    -> None
        | { EventType = WorkflowExecutionStarted(_, _, _, _, _, _, _, input, _, _) }::_ 
                -> input
        | _::tl -> getWorkflowInput tl

    /// Returns the event type associated with the specified Event ID
    let rec getEventTypeById eventId = function
        | { EventId = eventId'; EventType = eventType }::tl when eventId = eventId' -> eventType
        | hd::tl -> getEventTypeById eventId tl

// Represents an activity
type IActivity =
    abstract member Name        : string
    abstract member Description : string
    abstract member TaskList    : TaskList    
    abstract member TaskHeartbeatTimeout        : Seconds
    abstract member TaskScheduleToStartTimeout  : Seconds
    abstract member TaskStartToCloseTimeout     : Seconds
    abstract member TaskScheduleToCloseTimeout  : Seconds

    /// Processes a string input and returns the result
    abstract member Process     : string -> string

type Activity<'TInput, 'TOutput>(name, description, 
                                 processor                   : 'TInput -> 'TOutput,
                                 taskHeartbeatTimeout        : Seconds,
                                 taskScheduleToStartTimeout  : Seconds,
                                 taskStartToCloseTimeout     : Seconds,
                                 taskScheduleToCloseTimeout  : Seconds,
                                 ?taskList) =
    let taskList = defaultArg taskList (name + "TaskList")
    let inputSerializer  = JsonSerializer<'TInput>()
    let outputSerializer = JsonSerializer<'TOutput>()

    // use Json serializer to marshall the input and output from-and-to string
    let processor = inputSerializer.DeserializeFromString >> processor >> outputSerializer.SerializeToString
    
    // C# friendly constructor which takes in a Func instead of function
    new(name, description, processor : Func<'TInput, 'TOutput>,
        taskHeartbeatTimeout, taskScheduleToStartTimeout, taskStartToCloseTimeout, taskScheduleToCloseTimeout,
        ?taskList) = 
            Activity<'TInput, 'TOutput>(name, description, (fun input -> processor.Invoke(input)),
                                        taskHeartbeatTimeout,
                                        taskScheduleToStartTimeout,
                                        taskStartToCloseTimeout,
                                        taskScheduleToCloseTimeout,
                                        ?taskList = taskList)

    interface IActivity with
        member this.Name                        = name
        member this.Description                 = description
        member this.TaskList                    = TaskList(Name = taskList)
        member this.TaskHeartbeatTimeout        = taskHeartbeatTimeout
        member this.TaskScheduleToStartTimeout  = taskScheduleToStartTimeout
        member this.TaskStartToCloseTimeout     = taskStartToCloseTimeout
        member this.TaskScheduleToCloseTimeout  = taskScheduleToCloseTimeout

        member this.Process (input)             = processor input

type Activity = Activity<string, string>

and StageAction = 
    | ScheduleActivity      of IActivity
    | StartChildWorkflow    of Workflow

and Stage =
    {
        Id      : int
        Action  : StageAction
        Version : string
    }

and Workflow (domain, name, description, version, ?taskList, 
              ?stages                  : Stage list,
              ?taskStartToCloseTimeout : Seconds,
              ?execStartToCloseTimeout : Seconds,
              ?childPolicy             : ChildPolicy) =
    do if nullOrWs domain then nullArg "domain"
    do if nullOrWs name   then nullArg "name"

    let onDecisionTaskError = new Event<Exception>()
    let onActivityTaskError = new Event<Exception>()

    let taskList = new TaskList(Name = defaultArg taskList (name + "TaskList"))

    // sort the stages by Id
    let stages = defaultArg stages [] |> List.sortBy (fun { Id = id } -> id)

    /// registers the workflow and activity types
    let register (clt : Amazon.SimpleWorkflow.AmazonSimpleWorkflowClient) = 
        let registerActivities stages = async {
            let req = ListActivityTypesRequest(Domain = domain).WithRegistrationStatus(string Registered)
            let! res = clt.ListActivityTypesAsync(req)

            let existing = res.ListActivityTypesResult.ActivityTypeInfos.TypeInfos
                           |> Seq.map (fun info -> info.ActivityType.Name, info.ActivityType.Version)
                           |> Set.ofSeq

            let activities = stages 
                             |> List.choose (function 
                                | { Id = id; Action = ScheduleActivity(activity); Version = version } when not <| existing.Contains(activity.Name, version)
                                    -> Some(id, activity, version) 
                                | _ -> None)

            for (id, activity, version) in activities do
                let req = RegisterActivityTypeRequest(Domain = domain, Name = activity.Name)
                            .WithDescription(activity.Description)
                            .WithDefaultTaskList(activity.TaskList)
                            .WithVersion(version)
                            .WithDefaultTaskHeartbeatTimeout(str activity.TaskHeartbeatTimeout)
                            .WithDefaultTaskScheduleToStartTimeout(str activity.TaskScheduleToStartTimeout)
                            .WithDefaultTaskStartToCloseTimeout(str activity.TaskStartToCloseTimeout)
                            .WithDefaultTaskScheduleToCloseTimeout(str activity.TaskScheduleToCloseTimeout)

                do! clt.RegisterActivityTypeAsync(req) |> Async.Ignore
        }

        let registerWorkflow () = async {
            let req = ListWorkflowTypesRequest(Domain = domain, Name = name).WithRegistrationStatus(string Registered)
            let! res = clt.ListWorkflowTypesAsync(req)

            // only register the workflow if it doesn't exist already
            if res.ListWorkflowTypesResult.WorkflowTypeInfos.TypeInfos.Count = 0 then
                let req = RegisterWorkflowTypeRequest(Domain = domain, Name = name)
                            .WithDescription(description)
                            .WithVersion(version)
                            .WithDefaultTaskList(taskList)
                taskStartToCloseTimeout ?-> (str >> req.WithDefaultTaskStartToCloseTimeout)
                execStartToCloseTimeout ?-> (str >> req.WithDefaultExecutionStartToCloseTimeout)
                childPolicy             ?-> (str >> req.WithDefaultChildPolicy)

                do! clt.RegisterWorkflowTypeAsync(req) |> Async.Ignore
        }

        let registerDomain () = async {
            let req = ListDomainsRequest().WithRegistrationStatus(string Registered)
            let! res = clt.ListDomainsAsync(req)

            // only register the domain if it doesn't exist already
            let exists = res.ListDomainsResult.DomainInfos.Name |> Seq.exists (fun info -> info.Name = domain)
            if not <| exists then
                let req = RegisterDomainRequest(Name = domain)
                            .WithWorkflowExecutionRetentionPeriodInDays(string Constants.maxWorkflowExecRetentionPeriodInDays)
                do! clt.RegisterDomainAsync(req) |> Async.Ignore
        }

        // run the domain registration first, otherwise the activities and workflow registrations will fail!
        registerDomain() |> Async.RunSynchronously

        seq { yield registerActivities stages; yield registerWorkflow();  }
        |> Async.Parallel
        |> Async.RunSynchronously

    /// tries to get the nth (zero-index) stage
    let getStage n = if n >= stages.Length then None else List.nth stages n |> Some

    /// schedules the nth (zero-indexed) stage
    let scheduleStage n input =
        match getStage n with
        | Some({ Id = id; Action = ScheduleActivity(activity); Version = version }) 
               -> let activityType = ActivityType(Name = activity.Name, Version = version)
                  let decision = ScheduleActivityTask(str id, activityType,
                                                      Some activity.TaskList, 
                                                      input,
                                                      Some activity.TaskHeartbeatTimeout, 
                                                      Some activity.TaskScheduleToStartTimeout, 
                                                      Some activity.TaskStartToCloseTimeout, 
                                                      Some activity.TaskScheduleToCloseTimeout,
                                                      None)
                  [| decision |], ""
        | Some({ Id = id; Action = StartChildWorkflow(workflow) }) 
               -> let workflowType = WorkflowType(Name = workflow.Name, Version = workflow.Version)
                  let decision = StartChildWorkflowExecution(str id, workflowType,
                                                             workflow.ChildPolicy,
                                                             Some workflow.TaskList, 
                                                             input,
                                                             None,
                                                             workflow.ExecutionStartToCloseTimeout,
                                                             workflow.TaskStartToCloseTimeout,                                                             
                                                             Some(string id))
                  [| decision |], ""
        | None -> [| CompleteWorkflowExecution input |], ""

    let rec decide (events : HistoryEvent list) input =
        match events with
        | [] -> scheduleStage 0 input
        | { EventType = ActivityTaskCompleted(scheduledEvtId, _, result) }::tl ->
            // find the event for when the activity was scheduled
            let (ActivityTaskScheduled(activityId, _, _, _, _, _, _, _, _, _)) = HistoryEvents.getEventTypeById scheduledEvtId tl
            
            // schedule the next stage in the workflow
            scheduleStage (int activityId + 1) result
        | { EventType = ChildWorkflowExecutionCompleted(initiatedEvtId, _, workflowExec, _, result) }::tl ->            
            // find the event for when the child workflow was started
            let (StartChildWorkflowExecutionInitiated(_, _, _, _, _, _, _, control, _, _)) = HistoryEvents.getEventTypeById initiatedEvtId tl

            // schedule the next stage in the workflow
            let (Some(stageId)) = control            
            scheduleStage (int stageId + 1) result
        | { EventType = StartChildWorkflowExecutionInitiated _ }::tl ->
            // we're still waiting for a child workflow to finish, do nothing for now
            [||], ""
        | hd::tl -> decide tl input

    let decider (task : DecisionTask) = 
        let input = HistoryEvents.getWorkflowInput task.Events
        decide task.Events input

    let startDecisionWorker clt  = DecisionWorker.Start(clt, domain, taskList.Name, decider, onDecisionTaskError.Trigger)
    let startActivityWorkers (clt : Amazon.SimpleWorkflow.AmazonSimpleWorkflowClient) = 
        stages 
        |> List.choose (function | { Action = ScheduleActivity(activity) } -> Some activity | _ -> None)
        |> List.iter (fun activity -> 
            let heartbeat = TimeSpan.FromSeconds(float activity.TaskHeartbeatTimeout)
            ActivityWorker.Start(clt, domain, activity.TaskList.Name,
                                 activity.Process, onActivityTaskError.Trigger, 
                                 heartbeat))

    member private this.Append (toAction : 'a -> StageAction, item : 'a) = 
        let id = stages.Length
        let stage = { Id = id; Action = toAction item; Version = sprintf "%s.%d" name id }
        Workflow(domain, name, description, version, taskList.Name, 
                 stage :: stages,
                 ?taskStartToCloseTimeout = taskStartToCloseTimeout,
                 ?execStartToCloseTimeout = execStartToCloseTimeout,
                 ?childPolicy             = childPolicy)         

    [<CLIEvent>]
    member this.OnDecisionTaskError = onDecisionTaskError.Publish

    [<CLIEvent>]
    member this.OnActivityTaskError = onActivityTaskError.Publish

    member this.Name                         = name
    member this.Version                      = version
    member this.TaskList                     = taskList
    member this.TaskStartToCloseTimeout      = taskStartToCloseTimeout
    member this.ExecutionStartToCloseTimeout = execStartToCloseTimeout
    member this.ChildPolicy                  = childPolicy
    member this.NumberOfStages               = stages.Length

    member this.Start swfClt = 
        register swfClt |> ignore
        startDecisionWorker  swfClt
        startActivityWorkers swfClt

        // start any child workflows
        stages 
        |> List.choose (function | { Action = StartChildWorkflow(workflow) } -> Some workflow | _ -> None)
        |> List.iter (fun workflow -> workflow.Start swfClt)

    static member (++>) (workflow : Workflow, activity : IActivity) = workflow.Append(ScheduleActivity, activity)
    static member (++>) (workflow : Workflow, childWorkflow : Workflow) = 
        if childWorkflow.ExecutionStartToCloseTimeout.IsNone then failwithf "child workflows must specify execution timeout"
        if childWorkflow.TaskStartToCloseTimeout.IsNone then failwithf "child workflows must specify decision task timeout"
        if childWorkflow.ChildPolicy.IsNone then failwithf "child workflows must specify child policy"

        workflow.Append(StartChildWorkflow, childWorkflow)