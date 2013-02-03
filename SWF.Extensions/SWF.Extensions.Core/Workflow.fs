namespace Amazon.SimpleWorkflow.Extensions

open System
open Microsoft.FSharp.Collections

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Model
open ServiceStack.Text

open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Extensions.Model

// Marker interface for anything that can be scheduled at a stage of a workflow
type ISchedulable = 
    abstract member Name        : string
    abstract member Description : string
    abstract member MaxAttempts : int

// Represents an activity
type IActivity =
    inherit ISchedulable

    abstract member TaskList    : TaskList    
    abstract member TaskHeartbeatTimeout        : Seconds
    abstract member TaskScheduleToStartTimeout  : Seconds
    abstract member TaskStartToCloseTimeout     : Seconds
    abstract member TaskScheduleToCloseTimeout  : Seconds
    
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
        mutable StageNumber   : int
        mutable AttemptNumber : int
        mutable MaxAttempts   : int
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
    
    // identifies the key events which we need to respond to
    let (|KeyEvent|_|) historyEvt = 
        match historyEvt with
        | { EventType = ActivityTaskFailed _ } 
        | { EventType = ActivityTaskTimedOut _ } 
        | { EventType = ActivityTaskCompleted _ }
        | { EventType = ChildWorkflowExecutionFailed _ } 
        | { EventType = ChildWorkflowExecutionCompleted _ } 
        | { EventType = ChildWorkflowExecutionCompleted _ }
        | { EventType = StartChildWorkflowExecutionInitiated _ }
        | { EventType = WorkflowExecutionStarted _ }
            -> Some historyEvt.EventType
        | _ -> None

    // returns the event type associated with the specified event ID
    let findEventTypeById eventId (evts : HistoryEvent seq) =
        evts |> Seq.pick (function | { EventId = eventId'; EventType = eventType } when eventId = eventId' -> Some eventType | _ -> None)

    let getStageActionId actionName { StageNumber = stage; AttemptNumber = n } = 
        sprintf "%d.%s.attempt_%d.%s" stage actionName n (Guid.NewGuid().ToString().Substring(0, 8))
    let getActivityVersion stageNum = sprintf "%s.%d" name stageNum

    // sort the stages by Id
    let stages = defaultArg stages [] |> List.sortBy (fun { StageNumber = n } -> n)

    // all the activities (including those part of a schedule multiple action)
    let activities = stages |> List.collect (function 
            | { Action = ScheduleActivity(activity) } -> [ activity ]
            | _ -> [])

    // all the child workflows (including those part of a schedule multiple action)
    let childWorkflows = stages |> List.collect (function 
            | { Action = StartChildWorkflow(workflow) } -> [ workflow ]
            | _ -> [])
    
    /// tries to get the nth (zero-index) stage
    let getStage n = if n >= stages.Length then None else List.nth stages n |> Some

    // registers the workflow and activity types
    let register (clt : Amazon.SimpleWorkflow.AmazonSimpleWorkflowClient) = 
        let registerActivities stages = async {
            let req = ListActivityTypesRequest(Domain = domain).WithRegistrationStatus(string Registered)
            let! res = clt.ListActivityTypesAsync(req)

            let existing = res.ListActivityTypesResult.ActivityTypeInfos.TypeInfos
                           |> Seq.map (fun info -> info.ActivityType.Name, info.ActivityType.Version)
                           |> Set.ofSeq

            let activities = stages 
                             |> List.collect (function 
                                | { StageNumber = stageNum; Action = ScheduleActivity(activity) }
                                    -> [ (stageNum, activity, getActivityVersion stageNum) ]
                                | _ -> [])
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
    
    // schedules the nth (zero-indexed) stage
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
            let schedulable  = workflow :> ISchedulable
            let workflowType = WorkflowType(Name = schedulable.Name, Version = workflow.Version)
            let state        = { StageNumber = stageNum; AttemptNumber = attempts + 1; MaxAttempts = schedulable.MaxAttempts }
            let control      = state |> stageStateSerializer.SerializeToString
            let decision = StartChildWorkflowExecution(getStageActionId schedulable.Name state, 
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

    let rec decide (events : HistoryEvent seq) =
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
                -> onWorkflowFailed.Trigger(domain, (workflow :> ISchedulable).Name, actionId, details, reason)
            | _ -> ()

            if state.AttemptNumber >= state.MaxAttempts
            then [| FailWorkflowExecution(details, reason) |], ""
            else scheduleStage state.StageNumber input state.AttemptNumber // retry with the original input

        let keyEvt = events |> Seq.pick (function | KeyEvent evtType -> Some evtType | _ -> None)

        match keyEvt with
        | WorkflowExecutionStarted(_, _, _, _, _, _, _, input, _, _) -> scheduleStage 0 input 0

        // when activities and workflows completed/failed, we need to go back to the event that scheduled them in order
        // to get the control data we need to find out the state of that stage of the workflow in order to decide
        // on the appropriate next course of action
        | ActivityTaskFailed(scheduledEvtId, _, details, reason) ->
            let (ActivityTaskScheduled(activityId, _, _, _, control, input, _, _, _, _)) = findEventTypeById scheduledEvtId events
            failedStage control activityId input details reason

        | ActivityTaskTimedOut(scheduledEvtId, _, timeoutType, details) ->
            let (ActivityTaskScheduled(activityId, _, _, _, control, input, _, _, _, _)) = findEventTypeById scheduledEvtId events            
            failedStage control activityId input details (Some(sprintf "Timeout : %s" <| str timeoutType))

        | ActivityTaskCompleted(scheduledEvtId, _, result) ->
            let (ActivityTaskScheduled(_, _, _, _, control, _, _, _, _, _)) = findEventTypeById scheduledEvtId events
            nextStep control result

        | ChildWorkflowExecutionFailed(initiatedEvtId, _, workflowExec, _, details, reason) ->            
            let (StartChildWorkflowExecutionInitiated(_, _, _, _, _, _, input, control, _, _)) = findEventTypeById initiatedEvtId events
            failedStage control workflowExec.RunId input details reason

        | ChildWorkflowExecutionCompleted(initiatedEvtId, _, _, _, result) ->
            let (StartChildWorkflowExecutionInitiated(_, _, _, _, _, _, _, control, _, _)) = findEventTypeById initiatedEvtId events
            nextStep control result

        | StartChildWorkflowExecutionInitiated _ ->
            // we're still waiting for a child workflow to finish, do nothing for now
            [||], ""

    let decider (task : DecisionTask) = decide task.Events

    let startDecisionWorker clt  = DecisionWorker.Start(clt, domain, taskList.Name, decider, onDecisionTaskError.Trigger, ?identity = identity)
    let startActivityWorkers clt = 
        activities
        |> List.iter (fun activity -> 
            // leave some buffer for heart beat frequency, record a heartbeat every 75% of the timeout
            let heartbeat = TimeSpan.FromSeconds(float activity.TaskHeartbeatTimeout * 0.75)
            ActivityWorker.Start(clt, domain, activity.TaskList.Name, activity.Process, onActivityTaskError.Trigger, heartbeat, ?identity = identity))
    let startChildWorkflows clt = childWorkflows |> List.iter (fun workflow -> workflow.Start clt)

    static let validateChildWorkflow (child : Workflow) =
        if child.ExecutionStartToCloseTimeout.IsNone then failwithf "child workflows must specify execution timeout"
        if child.TaskStartToCloseTimeout.IsNone then failwithf "child workflows must specify decision task timeout"
        if child.ChildPolicy.IsNone then failwithf "child workflows must specify child policy"

    member private this.Append (toAction : 'a -> StageAction, item : 'a) = 
        let id = stages.Length
        let stage = { StageNumber = id; Action = toAction item }
        Workflow(domain, name, description, version, taskList.Name, 
                 stage :: stages,
                 ?taskStartToCloseTimeout = taskStartToCloseTimeout,
                 ?execStartToCloseTimeout = execStartToCloseTimeout,
                 ?childPolicy             = childPolicy,
                 ?identity                = identity)         
                 
    member this.Start swfClt = 
        register swfClt |> ignore
        startDecisionWorker  swfClt
        startActivityWorkers swfClt
        startChildWorkflows  swfClt        

    // #region Public Events

    [<CLIEvent>] member this.OnDecisionTaskError = onDecisionTaskError.Publish
    [<CLIEvent>] member this.OnActivityTaskError = onActivityTaskError.Publish
    [<CLIEvent>] member this.OnActivityFailed    = onActivityFailed.Publish
    [<CLIEvent>] member this.OnWorkflowFailed    = onWorkflowFailed.Publish
    [<CLIEvent>] member this.OnWorkflowCompleted = onWorkflowCompleted.Publish

    // #endregion
         
    interface ISchedulable with
        member this.Name                     = name
        member this.Description              = description
        member this.MaxAttempts              = maxAttempts

    member this.Version                      = version
    member this.TaskList                     = taskList
    member this.TaskStartToCloseTimeout      = taskStartToCloseTimeout
    member this.ExecutionStartToCloseTimeout = execStartToCloseTimeout
    member this.ChildPolicy                  = childPolicy
    
    member this.NumberOfStages               = stages.Length

    // #region Operators

    static member (++>) (workflow : Workflow, activity : IActivity) = workflow.Append(ScheduleActivity, activity)
    static member (++>) (workflow : Workflow, childWorkflow : Workflow) =         
        validateChildWorkflow childWorkflow
        workflow.Append(StartChildWorkflow, childWorkflow)

    // #endregion