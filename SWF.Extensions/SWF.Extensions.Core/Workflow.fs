module SWF.Extensions.Core.Workflow

open System

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Model

open SWF.Extensions.Core.Model

type Activity (name, description : string, task : string -> string, 
               taskHeartbeatTimeout        : Seconds,
               taskScheduleToStartTimeout  : Seconds,
               taskStartToCloseTimeout     : Seconds,
               taskScheduleToCloseTimeout  : Seconds,
               ?taskList) = 
    let taskList = defaultArg taskList (name + "TaskList")
    
    member this.Name                        = name
    member this.Description                 = description
    member this.Task                        = task
    member this.TaskList                    = TaskList(Name = taskList)
    member this.TaskHeartbeatTimeout        = taskHeartbeatTimeout
    member this.TaskScheduleToStartTimeout  = taskScheduleToStartTimeout
    member this.TaskStartToCloseTimeout     = taskStartToCloseTimeout
    member this.TaskScheduleToCloseTimeout  = taskScheduleToCloseTimeout

type StageAction = 
    | ScheduleActivity  of Activity

type Stage =
    {
        Id      : int
        Action  : StageAction
        Version : string
    }
    
[<AutoOpen>]
module Helper =
    let nullOrWs = String.IsNullOrWhiteSpace

type Workflow (domain, name, description, version, ?taskList, 
               ?activities              : Activity list,
               ?taskStartToCloseTimeout : Seconds,
               ?execStartToCloseTimeout : Seconds,
               ?childPolicy             : ChildPolicy) =
    do if nullOrWs domain then nullArg "domain"
    do if nullOrWs name   then nullArg "name"

    let taskList = new TaskList(Name = defaultArg taskList (name + "TaskList"))
    let activities = defaultArg activities []
    let stages = activities 

                 |> List.mapi (fun i activity -> { Id = i; Action = ScheduleActivity activity; Version = sprintf "%s.%d" name i })

    /// registers the workflow and activity types
    let register (clt : Amazon.SimpleWorkflow.AmazonSimpleWorkflowClient) = 
        let registerActivities stages =
            stages 
            |> List.choose (function | { Id = id; Action = ScheduleActivity(activity); Version = version } -> Some(id, activity, version) | _ -> None)
            |> List.map (fun (id, activity, version) -> 
                let req = RegisterActivityTypeRequest(Domain = domain, Name = activity.Name)
                            .WithDescription(activity.Description)
                            .WithDefaultTaskList(activity.TaskList)
                            .WithVersion(version)
                            .WithDefaultTaskHeartbeatTimeout(str activity.TaskHeartbeatTimeout)
                            .WithDefaultTaskScheduleToStartTimeout(str activity.TaskScheduleToStartTimeout)
                            .WithDefaultTaskStartToCloseTimeout(str activity.TaskStartToCloseTimeout)
                            .WithDefaultTaskScheduleToCloseTimeout(str activity.TaskScheduleToCloseTimeout)

                async { do! clt.RegisterActivityTypeAsync(req) |> Async.Ignore })

        let registerWorkflow () =
            let req = RegisterWorkflowTypeRequest(Domain = domain, Name = name)
                        .WithDescription(description)
                        .WithVersion(version)
                        .WithDefaultTaskList(taskList)
            taskStartToCloseTimeout ?-> (str >> req.WithDefaultTaskStartToCloseTimeout)
            execStartToCloseTimeout ?-> (str >> req.WithDefaultExecutionStartToCloseTimeout)
            childPolicy             ?-> (str >> req.WithDefaultChildPolicy)

            async { do! clt.RegisterWorkflowTypeAsync(req) |> Async.Ignore }

        seq { yield! registerActivities stages; yield registerWorkflow() }
        |> Async.Parallel
        |> Async.RunSynchronously

    /// recursively tries to get the history event with the specified event Id
    let rec getEventType eventId = function
        | { EventId = eventId'; EventType = eventType }::tl when eventId = eventId' -> eventType
        | hd::tl -> getEventType eventId tl

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
        | None -> [| CompleteWorkflowExecution None |], ""

    let rec decide (events : HistoryEvent list) =
        match events with
        | [] -> scheduleStage 0 None
        | { EventType = ActivityTaskCompleted(scheduledEvtId, _, result) }::tl ->
            // find the event for when the activity was scheduled
            let (ActivityTaskScheduled(activityId, _, _, _, _, _, _, _, _, _)) = getEventType scheduledEvtId tl
            
            // schedule the next stage in the workflow
            scheduleStage (int activityId + 1) result
        | hd::tl -> decide tl

    let decider (task : DecisionTask) = decide task.Events

    let startDecisionWorker clt  = DecisionWorker.Start(clt, domain, taskList.Name, decider, (fun _ -> ()))
    let startActivityWorkers (clt : Amazon.SimpleWorkflow.AmazonSimpleWorkflowClient) = 
        stages 
        |> List.choose (function | { Action = ScheduleActivity(activity) } -> Some activity | _ -> None)
        |> List.iter (fun activity -> 
            let heartbeat = TimeSpan.FromSeconds(float activity.TaskHeartbeatTimeout)
            ActivityWorker.Start(clt, domain, activity.TaskList.Name,
                                 activity.Task, (fun _ -> ()), 
                                 heartbeat))

    member private this.Attach(activity) = Workflow(domain, name, description, version, taskList.Name, 
                                                    activity :: activities |> List.rev,
                                                    ?taskStartToCloseTimeout = taskStartToCloseTimeout,
                                                    ?execStartToCloseTimeout = execStartToCloseTimeout,
                                                    ?childPolicy             = childPolicy)

    member this.Register swfClt = 
        // register the workflow type and activity types
        register swfClt |> ignore

    member this.Start swfClt = 
        startDecisionWorker  swfClt
        startActivityWorkers swfClt

    static member (++>) (workflow : Workflow, activity) = workflow.Attach(activity)