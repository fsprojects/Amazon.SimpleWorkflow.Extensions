namespace Amazon.SimpleWorkflow.Extensions

open System

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Model
open ServiceStack.Text

open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Extensions.Model

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
    abstract member MaxAttempts                 : int

    /// Processes a string input and returns the result
    abstract member Process     : string -> string

/// Represents an activity, which in essence, is a function that takes some input and 
/// returns some output
type Activity<'TInput, 'TOutput>(name, description, 
                                 processor                   : 'TInput -> 'TOutput,
                                 taskHeartbeatTimeout        : Seconds,
                                 taskScheduleToStartTimeout  : Seconds,
                                 taskStartToCloseTimeout     : Seconds,
                                 taskScheduleToCloseTimeout  : Seconds,
                                 ?taskList,
                                 ?maxAttempts) =
    let taskList    = defaultArg taskList (name + "TaskList")
    let maxAttempts = defaultArg maxAttempts 1    // by default, only attempt once, i.e. no retry

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
        member this.MaxAttempts                 = maxAttempts

        member this.Process (input)             = processor input

type Activity = Activity<string, string>

type StageExecutionState =
    {
        StageNumber   : int
        AttemptNumber : int
        MaxAttempts   : int
    }

/// The different actions that can be taken as a stage in the workflow
type StageAction = 
    | ScheduleActivity      of IActivity
    | StartChildWorkflow    of Workflow

/// Represents a stage in the workflow
and Stage =
    {
        StageNumber : int           // zero-based index, e.g. 5 = 6th stage
        Action      : StageAction   // what action to perform at this stage in the workflow?
    }

and Workflow (domain, name, description, version, ?taskList, 
              ?stages                  : Stage list,
              ?taskStartToCloseTimeout : Seconds,
              ?execStartToCloseTimeout : Seconds,
              ?childPolicy             : ChildPolicy,
              ?identity                : Identity,
              ?maxAttempts) =
    do if nullOrWs domain then nullArg "domain"
    do if nullOrWs name   then nullArg "name"

    let taskList = new TaskList(Name = defaultArg taskList (name + "TaskList"))
    let maxAttempts = defaultArg maxAttempts 1    // by default, only attempt once, i.e. no retry

    let onDecisionTaskError = new Event<Exception>()
    let onActivityTaskError = new Event<Exception>()
    let onActivityFailed    = new Event<Domain * Name * ActivityId * Details option * Reason option>()
    let onWorkflowFailed    = new Event<Domain * Name * RunId * Details option * Reason option>()
    let onWorkflowCompleted = new Event<Domain * Name>()

    static let stageStateSerializer = JsonSerializer<StageExecutionState>()

    let getStageActionId actionName { StageNumber = stage; AttemptNumber = n } = sprintf "%d.%s.attempt_%d" stage actionName n
    let getActivityVersion stageNum = sprintf "%s.%d" name stageNum

    // sort the stages by Id
    let stages = defaultArg stages [] |> List.sortBy (fun { StageNumber = n } -> n)
    
    /// tries to get the nth (zero-index) stage
    let getStage n = if n >= stages.Length then None else List.nth stages n |> Some

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
                                | { StageNumber = stageNum; Action = ScheduleActivity(activity) }
                                    -> Some(stageNum, activity, getActivityVersion stageNum)
                                | _ -> None)
                             |> List.filter (fun (_, activity, version) -> not <| existing.Contains(activity.Name, version))

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
    
    /// schedules the nth (zero-indexed) stage
    let scheduleStage n input attempts =
        let scheduleActivity stageNum (activity : IActivity) = 
            let activityType = ActivityType(Name = activity.Name, Version = getActivityVersion stageNum)
            let state        = { StageNumber = stageNum; AttemptNumber = attempts + 1; MaxAttempts = activity.MaxAttempts }
            let control      = state |> stageStateSerializer.SerializeToString
            let decision = ScheduleActivityTask(getStageActionId activity.Name state, 
                                                activityType,
                                                Some activity.TaskList, 
                                                input,
                                                Some activity.TaskHeartbeatTimeout, 
                                                Some activity.TaskScheduleToStartTimeout, 
                                                Some activity.TaskStartToCloseTimeout, 
                                                Some activity.TaskScheduleToCloseTimeout,
                                                Some control)
            [| decision |], ""

        let scheduleChildWorkflow stageNum (workflow : Workflow) =
            let workflowType = WorkflowType(Name = workflow.Name, Version = workflow.Version)
            let state        = { StageNumber = stageNum; AttemptNumber = attempts + 1; MaxAttempts = workflow.MaxAttempts }
            let control      = state |> stageStateSerializer.SerializeToString
            let decision = StartChildWorkflowExecution(getStageActionId workflow.Name state, 
                                                       workflowType,
                                                       workflow.ChildPolicy,
                                                       Some workflow.TaskList, 
                                                       input,
                                                       None,
                                                       workflow.ExecutionStartToCloseTimeout,
                                                       workflow.TaskStartToCloseTimeout,                                                             
                                                       Some control)
            [| decision |], ""

        match getStage n with
        | Some({ StageNumber = stageNum; Action = ScheduleActivity(activity) }) 
               -> scheduleActivity stageNum activity                
        | Some({ StageNumber = stageNum; Action = StartChildWorkflow(workflow) }) 
               -> scheduleChildWorkflow stageNum workflow
        | None -> onWorkflowCompleted.Trigger(domain, name)
                  [| CompleteWorkflowExecution input |], ""

    let rec decide (events : HistoryEvent list) input =
        // makes the next move based on the control data for the previous step and its result
        let nextStep (Some control) result =
            let state = stageStateSerializer.DeserializeFromString control
            scheduleStage (state.StageNumber + 1) result 0 // 0 = first attempt (0 attempts so far)

        // defailed wheter to fail the workflow or retry the stage
        let failedStage (Some control) actionId input details reason =
            let state = stageStateSerializer.DeserializeFromString control

            // get the stage that failed, raise the appropriate event
            let stage = getStage state.StageNumber
            match stage with
            | Some { Action = ScheduleActivity(activity) } 
                -> onActivityFailed.Trigger(domain, activity.Name, actionId, details, reason)
            | Some { Action = StartChildWorkflow(workflow) }
                -> onWorkflowFailed.Trigger(domain, workflow.Name, actionId, details, reason)
            | _ -> ()

            if state.AttemptNumber >= state.MaxAttempts
            then [| FailWorkflowExecution(details, reason) |], ""
            else scheduleStage state.StageNumber input state.AttemptNumber // retry with the original input

        match events with
        | [] -> scheduleStage 0 input 0
        // when activities and workflows completed/failed, we need to go back to the event that scheduled them in order
        // to get the control data we need to find out the state of that stage of the workflow in order to decide
        // on the appropriate next course of action
        | { EventType = ActivityTaskFailed(scheduledEvtId, _, details, reason) }::tl ->
            let (ActivityTaskScheduled(activityId, _, _, _, control, input, _, _, _, _)) = HistoryEvents.getEventTypeById scheduledEvtId tl            
            failedStage control activityId input details reason

        | { EventType = ActivityTaskTimedOut(scheduledEvtId, _, timeoutType, details) }::tl ->
            let (ActivityTaskScheduled(activityId, _, _, _, control, input, _, _, _, _)) = HistoryEvents.getEventTypeById scheduledEvtId tl            
            failedStage control activityId input details (Some(sprintf "Timeout : %s" <| str timeoutType))

        | { EventType = ActivityTaskCompleted(scheduledEvtId, _, result) }::tl ->
            let (ActivityTaskScheduled(_, _, _, _, control, _, _, _, _, _)) = HistoryEvents.getEventTypeById scheduledEvtId tl
            nextStep control result

        | { EventType = ChildWorkflowExecutionFailed(initiatedEvtId, _, workflowExec, _, details, reason) }::tl ->            
            let (StartChildWorkflowExecutionInitiated(_, _, _, _, _, _, input, control, _, _)) = HistoryEvents.getEventTypeById initiatedEvtId tl
            failedStage control workflowExec.RunId input details reason

        | { EventType = ChildWorkflowExecutionCompleted(initiatedEvtId, _, _, _, result) }::tl ->
            let (StartChildWorkflowExecutionInitiated(_, _, _, _, _, _, _, control, _, _)) = HistoryEvents.getEventTypeById initiatedEvtId tl
            nextStep control result

        | { EventType = StartChildWorkflowExecutionInitiated _ }::tl ->
            // we're still waiting for a child workflow to finish, do nothing for now
            [||], ""
        | hd::tl -> decide tl input

    let decider (task : DecisionTask) = 
        let input = HistoryEvents.getWorkflowInput task.Events
        decide task.Events input

    let startDecisionWorker clt  = DecisionWorker.Start(clt, domain, taskList.Name, decider, onDecisionTaskError.Trigger, ?identity = identity)
    let startActivityWorkers (clt : Amazon.SimpleWorkflow.AmazonSimpleWorkflowClient) = 
        stages 
        |> List.choose (function | { Action = ScheduleActivity(activity) } -> Some activity | _ -> None)
        |> List.iter (fun activity -> 
            let heartbeat = TimeSpan.FromSeconds(float activity.TaskHeartbeatTimeout)
            ActivityWorker.Start(clt, domain, activity.TaskList.Name, activity.Process, onActivityTaskError.Trigger, heartbeat, ?identity = identity))

    member private this.Append (toAction : 'a -> StageAction, item : 'a) = 
        let id = stages.Length
        let stage = { StageNumber = id; Action = toAction item }
        Workflow(domain, name, description, version, taskList.Name, 
                 stage :: stages,
                 ?taskStartToCloseTimeout = taskStartToCloseTimeout,
                 ?execStartToCloseTimeout = execStartToCloseTimeout,
                 ?childPolicy             = childPolicy,
                 ?identity                = identity)         

    // #region public Events

    [<CLIEvent>] member this.OnDecisionTaskError = onDecisionTaskError.Publish
    [<CLIEvent>] member this.OnActivityTaskError = onActivityTaskError.Publish
    [<CLIEvent>] member this.OnActivityFailed    = onActivityFailed.Publish
    [<CLIEvent>] member this.OnWorkflowFailed    = onWorkflowFailed.Publish
    [<CLIEvent>] member this.OnWorkflowCompleted = onWorkflowCompleted.Publish

    // #endregion

    member this.Name                         = name
    member this.Version                      = version
    member this.TaskList                     = taskList
    member this.TaskStartToCloseTimeout      = taskStartToCloseTimeout
    member this.ExecutionStartToCloseTimeout = execStartToCloseTimeout
    member this.ChildPolicy                  = childPolicy
    member this.MaxAttempts                  = maxAttempts
    member this.NumberOfStages               = stages.Length    

    member this.Start swfClt = 
        register swfClt |> ignore
        startDecisionWorker  swfClt
        startActivityWorkers swfClt

        // start any child workflows
        stages 
        |> List.choose (function | { Action = StartChildWorkflow(workflow) } -> Some workflow | _ -> None)
        |> List.iter (fun workflow -> workflow.Start swfClt)

    // #region Operators

    static member (++>) (workflow : Workflow, activity : IActivity) = workflow.Append(ScheduleActivity, activity)
    static member (++>) (workflow : Workflow, childWorkflow : Workflow) = 
        if childWorkflow.ExecutionStartToCloseTimeout.IsNone then failwithf "child workflows must specify execution timeout"
        if childWorkflow.TaskStartToCloseTimeout.IsNone then failwithf "child workflows must specify decision task timeout"
        if childWorkflow.ChildPolicy.IsNone then failwithf "child workflows must specify child policy"

        workflow.Append(StartChildWorkflow, childWorkflow)

    // #endregion