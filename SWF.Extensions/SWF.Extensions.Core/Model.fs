module Amazon.SimpleWorkflow.Extensions.Model

open System
open System.Collections.Generic

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Model
open Amazon.SimpleWorkflow.Extensions

// #region type alias

type ActivityId             = string
type Cause                  = string
type Control                = string
type Description            = string
type Details                = string
type Domain                 = string
type EventId                = int64
type ExecutionContext       = string
type Identity               = string
type Input                  = string
type MarkerName             = string
type Name                   = string
type Reason                 = string
type Result                 = string
type RunId                  = string
type Seconds                = int
type SignalName             = string
type SwfChildPolicy         = Amazon.SimpleWorkflow.ChildPolicy
type SwfDecision            = Amazon.SimpleWorkflow.Model.Decision
type SwfRegistrationStatus  = Amazon.SimpleWorkflow.RegistrationStatus
type TagList                = string[]
type TimerId                = string
type Version                = string
type WorkflowId             = string

// #endregion

exception UnknownTimeoutType of string
exception UnknownEventType   of string
exception UnknownChildPolicy of string

type RegistrationStatus = 
    | Registered
    | Deprecated

    override this.ToString() =
        match this with
        | Registered -> "REGISTERED"
        | Deprecated -> "DEPRECATED"

type ChildPolicy =
    | Terminate
    | RequestCancel
    | Abandon

    override this.ToString() =
        match this with
        | Terminate     -> "TERMINATE"
        | RequestCancel -> "REQUEST_CANCEL"
        | Abandon       -> "ABANDON"

    static member op_Explicit = function
        | "TERMINATE"       -> Terminate
        | "REQUEST_CANCEL"  -> RequestCancel
        | "ABANDON"         -> Abandon
        | str               -> raise <| UnknownChildPolicy str

type TimeoutType =
    | StartToClose
    | ScheduleToStart
    | ScheduleToClose
    | Heartbeat

    override this.ToString() =
        match this with
        | StartToClose      -> "START_TO_CLOSE"
        | ScheduleToStart   -> "SCHEDULE_TO_START"
        | ScheduleToClose   -> "SCHEDULE_TO_CLOSE"
        | Heartbeat         -> "HEARTBEAT"

    static member op_Explicit = function
        | "START_TO_CLOSE"      -> StartToClose
        | "SCHEDULE_TO_START"   -> ScheduleToStart
        | "SCHEDULE_TO_CLOSE"   -> ScheduleToClose
        | "HEARTBEAT"           -> Heartbeat
        | str                   -> raise <| UnknownTimeoutType str

// #region Convertor functions

let inline eventIdOp x      = (int64 >> function | 0L -> None | x' -> Some x') x
let inline secondsOp x      = (int >> function | 0 -> None | x' -> Some x') x
let inline seconds x        = int x
let inline timeout x        = TimeoutType.op_Explicit x
let inline childPolicy x    = ChildPolicy.op_Explicit x
let inline swfChildPolicy x = 
    match x with
    | Terminate     -> SwfChildPolicy.TERMINATE
    | RequestCancel -> SwfChildPolicy.REQUEST_CANCEL
    | Abandon       -> SwfChildPolicy.ABANDON
let inline swfRegStatus x   =
    match x with
    | Registered -> SwfRegistrationStatus.REGISTERED
    | Deprecated -> SwfRegistrationStatus.DEPRECATED

let inline tagListOp (lst : List<string>) = 
    match lst with 
    | null                 -> None
    | _ when lst.Count = 0 -> None 
    | _                    -> Some(lst.ToArray())

// #endregion

/// The different types of decisions that a decision worker can respond to a decision task with
/// see http://docs.aws.amazon.com/amazonswf/latest/apireference/API_Decision.html
type Decision =
    | CancelTimer                               of TimerId
    | CancelWorkflowExecution                   of Details option
    | CompleteWorkflowExecution                 of Result option
    // ContinueAsNewWorkflowExecution args:
    //      child policy
    //      task list
    //      input
    //      tag list
    //      execution start to close timeout
    //      task start to close timeout
    //      workflow type version
    | ContinueAsNewWorkflowExecution            of ChildPolicy option * TaskList option * Input option * TagList option * Seconds option * Seconds option * Version option
    | FailWorkflowExecution                     of Details option * Reason option
    | RecordMarker                              of MarkerName * Details option
    | RequestCancelActivityTask                 of ActivityId
    | RequestCancelExternalWorkflowExecution    of WorkflowId * RunId option * Control option
    // ScheduleActivityTask args: 
    //      activity Id
    //      activity type
    //      task list
    //      input
    //      heartbeat timeout
    //      schedule to start timeout
    //      start to close timeout
    //      schedule to close timeout
    //      control data
    | ScheduleActivityTask                      of ActivityId * ActivityType * TaskList option * Input option * Seconds option * Seconds option * Seconds option * Seconds option * Control option
    | SignalExternalWorkflowExecution           of WorkflowId * SignalName * Input option * RunId option * Control option
    // StartChildWorkflowExecution args:
    //      workflow Id
    //      workflow type
    //      child policy
    //      task list
    //      input
    //      tag list
    //      execution start to close timeout
    //      task start to close timeout
    //      control
    | StartChildWorkflowExecution               of WorkflowId * WorkflowType * ChildPolicy option * TaskList option * Input option * TagList option * Seconds option * Seconds option * Control option
    // StartTimer args:
    //      timer Id
    //      start to fire timeout
    //      control
    | StartTimer                                of TimerId * Seconds * Control option

    /// The type of decision as specified in the SWF API
    /// see http://docs.aws.amazon.com/amazonswf/latest/apireference/API_Decision.html
    member this.DecisionType = 
        match this with
        | CancelTimer _                             -> DecisionType.CancelTimer
        | CancelWorkflowExecution _                 -> DecisionType.CancelWorkflowExecution
        | CompleteWorkflowExecution _               -> DecisionType.CompleteWorkflowExecution
        | ContinueAsNewWorkflowExecution _          -> DecisionType.ContinueAsNewWorkflowExecution
        | FailWorkflowExecution _                   -> DecisionType.FailWorkflowExecution
        | RecordMarker _                            -> DecisionType.RecordMarker
        | RequestCancelActivityTask _               -> DecisionType.RequestCancelActivityTask
        | RequestCancelExternalWorkflowExecution _  -> DecisionType.RequestCancelExternalWorkflowExecution
        | ScheduleActivityTask _                    -> DecisionType.ScheduleActivityTask
        | SignalExternalWorkflowExecution _         -> DecisionType.SignalExternalWorkflowExecution
        | StartChildWorkflowExecution _             -> DecisionType.StartChildWorkflowExecution
        | StartTimer _                              -> DecisionType.StartTimer
    
    /// Returns a corresponding instance of Decision as defined in the AWSSDK
    member this.ToSwfDecision () =
        let decision = new SwfDecision(DecisionType = this.DecisionType)
        match this with
        | CancelTimer timerId
            -> let attrs = CancelTimerDecisionAttributes(TimerId = timerId)
               decision.CancelTimerDecisionAttributes <- attrs
        | CancelWorkflowExecution details
            -> let attrs = CancelWorkflowExecutionDecisionAttributes()
               <@ attrs.Details @>                                  <-? details
               decision.CancelWorkflowExecutionDecisionAttributes   <-  attrs
        | CompleteWorkflowExecution result
            -> let attrs = CompleteWorkflowExecutionDecisionAttributes()
               <@ attrs.Result @>                                   <-? result
               decision.CompleteWorkflowExecutionDecisionAttributes <-  attrs
        | ContinueAsNewWorkflowExecution(childPolicy, taskList, input, tagList, execTimeout, taskTimeout, version)
            -> let attrs = ContinueAsNewWorkflowExecutionDecisionAttributes()
               <@ attrs.ChildPolicy @>                  <-? (childPolicy ?>> swfChildPolicy)
               <@ attrs.TaskList @>                     <-? taskList
               <@ attrs.Input @>                        <-? input
               <@ attrs.ExecutionStartToCloseTimeout @> <-? (execTimeout ?>> str)
               <@ attrs.TaskStartToCloseTimeout @>      <-? (taskTimeout ?>> str)
               <@ attrs.WorkflowTypeVersion @>          <-? version
               attrs.TagList.AddRange                   <| defaultArg tagList [||]

               decision.ContinueAsNewWorkflowExecutionDecisionAttributes <- attrs
        | FailWorkflowExecution(details, reason)
            -> let attrs = FailWorkflowExecutionDecisionAttributes()
               <@ attrs.Details @>  <-? details
               <@ attrs.Reason @>   <-? reason
               decision.FailWorkflowExecutionDecisionAttributes <- attrs
        | RecordMarker(markerName, details)
            -> let attrs = RecordMarkerDecisionAttributes(MarkerName = markerName)
               <@ attrs.Details @>  <-? details
               decision.RecordMarkerDecisionAttributes <- attrs
        | RequestCancelActivityTask activityId
            -> let attrs = RequestCancelActivityTaskDecisionAttributes(ActivityId = activityId)
               decision.RequestCancelActivityTaskDecisionAttributes <- attrs
        | RequestCancelExternalWorkflowExecution(workflowId, runId, control) 
            -> let attrs = RequestCancelExternalWorkflowExecutionDecisionAttributes(WorkflowId = workflowId)
               <@ attrs.RunId @>    <-? runId
               <@ attrs.Control @>  <-? control
               decision.RequestCancelExternalWorkflowExecutionDecisionAttributes <- attrs
        | ScheduleActivityTask(activityId, activityType, taskList, input, heartbeatTimeout, schedToStartTimeout, timeout, schedToCloseTimeout, control)
            -> let attrs = ScheduleActivityTaskDecisionAttributes(ActivityId   = activityId,
                                                                  ActivityType = activityType)               
               <@ attrs.TaskList @>                 <-? taskList
               <@ attrs.Input @>                    <-? input
               <@ attrs.HeartbeatTimeout @>         <-? (heartbeatTimeout ?>> str)
               <@ attrs.ScheduleToStartTimeout @>   <-? (schedToStartTimeout ?>> str)
               <@ attrs.StartToCloseTimeout @>      <-? (timeout ?>> str)
               <@ attrs.ScheduleToCloseTimeout @>   <-? (schedToCloseTimeout  ?>> str)
               <@ attrs.Control @>                  <-? control

               decision.ScheduleActivityTaskDecisionAttributes <- attrs
        | SignalExternalWorkflowExecution(workflowId, signalName, input, runId, control)
            -> let attrs = SignalExternalWorkflowExecutionDecisionAttributes(WorkflowId = workflowId,
                                                                             SignalName = signalName)
               <@ attrs.Input @>    <-? input
               <@ attrs.RunId @>    <-? runId
               <@ attrs.Control @>  <-? control

               decision.SignalExternalWorkflowExecutionDecisionAttributes <- attrs
        | StartChildWorkflowExecution(workflowId, workflowType, childPolicy, taskList, input, tagList, execTimeout, taskTimeout, control)
            -> let attrs = StartChildWorkflowExecutionDecisionAttributes(WorkflowId   = workflowId,
                                                                         WorkflowType = workflowType)
               <@ attrs.ChildPolicy @>                  <-? (childPolicy ?>> swfChildPolicy)
               <@ attrs.TaskList @>                     <-? taskList
               <@ attrs.Input @>                        <-? input               
               <@ attrs.ExecutionStartToCloseTimeout @> <-? (execTimeout ?>> str)
               <@ attrs.TaskStartToCloseTimeout @>      <-? (taskTimeout ?>> str)
               <@ attrs.Control @>                      <-? control
               attrs.TagList.AddRange                   <| defaultArg tagList [||]

               decision.StartChildWorkflowExecutionDecisionAttributes <- attrs
        | StartTimer(timerId, startToFireTimeout, control)
            -> let attrs = StartTimerDecisionAttributes(TimerId            = timerId,
                                                        StartToFireTimeout = str startToFireTimeout)
               <@ attrs.Control @> <-? control

               decision.StartTimerDecisionAttributes <- attrs

        decision

/// The different types of event types that can be returned along with a decision task
/// see http://docs.aws.amazon.com/amazonswf/latest/apireference/API_HistoryEvent.html
type EventType =
    // ActivityTaskCanceled args: 
    //      scheduled event Id
    //      started event Id
    //      details of cancellation
    //      latest cancel requested event Id
    | ActivityTaskCanceled              of EventId * EventId * Details option * EventId option
    // ActivityTaskCancelRequested args:
    //      activity Id
    //      decision task completed event Id
    | ActivityTaskCancelRequested       of ActivityId * EventId
    // ActivityTaskCompleted args:
    //      scheduled event Id
    //      started event Id
    //      result of the task
    | ActivityTaskCompleted             of EventId * EventId * Result option
    // ActivityTaskFailed args:
    //      scheduled event Id
    //      started event Id
    //      details of the failure
    //      reason of the failure
    | ActivityTaskFailed                of EventId * EventId * Details option * Reason option
    // ActivityTaskScheduled args:
    //      activity Id
    //      activity type
    //      decision task completed event Id
    //      task list
    //      control
    //      input
    //      heartbeat timeout
    //      schedule to start timeout
    //      start to close timeout
    //      schedule to close timeout
    | ActivityTaskScheduled             of ActivityId * ActivityType * EventId * TaskList * Control option * Input option * Seconds option * Seconds option * Seconds option * Seconds option
    // ActivityTaskStarted args:
    //      scheduled event Id
    //      identity of the worker assigned the task
    | ActivityTaskStarted               of EventId * Identity option
    // ActivityTaskTimedOut args:
    //      scheduled event Id
    //      started event Id
    //      timeout type
    //      details parameter of the last RecordActivityTaskHeartbeat call
    | ActivityTaskTimedOut              of EventId * EventId * TimeoutType * Details option
    // CancelTimerFailed args:
    //      decision task completed event Id
    //      timer Id provided in the failed CancelTimer decision
    //      the cause of the failure
    | CancelTimerFailed                 of EventId * TimerId * Cause
    // CancelWorkflowExecutionFailed args:
    //      decision task completed event Id
    //      the cause of the failure
    | CancelWorkflowExecutionFailed     of EventId * Cause
    // ChildWorkflowExecutionCanceled args:
    //      start child workflow execution initiated event Id
    //      child workflow execution started event Id
    //      the child workflow that was cancelled
    //      the type of the child workflow
    //      details of the cancellation
    | ChildWorkflowExecutionCanceled    of EventId * EventId * WorkflowExecution * WorkflowType * Details option
    // ChildWorkflowExecutionCompleted args:
    //      start child workflow execution initiated event Id
    //      child workflow execution started event Id
    //      the child workflow that was completed
    //      the type of the child workflow
    //      the result of the child workflow
    | ChildWorkflowExecutionCompleted   of EventId * EventId * WorkflowExecution * WorkflowType * Result option
    // ChildWorkflowExecutionFailed args:
    //      start child workflow execution initiated event Id
    //      child workflow execution started event Id
    //      the child workflow that failed
    //      the type of the child workflow
    //      the reason of the failure
    //      the details of the failure    
    | ChildWorkflowExecutionFailed      of EventId * EventId * WorkflowExecution * WorkflowType * Details option * Reason option
    // ChildWorkflowExecutionStarted args:
    //      start child workflow execution initiated event Id
    //      the child workflow that was started
    //      the type of the child workflow
    | ChildWorkflowExecutionStarted     of EventId * WorkflowExecution * WorkflowType
    // ChildWorkflowExecutionTerminated args:
    //      start child workflow execution initiated event Id
    //      child workflow execution started event Id
    //      the child workflow that was terminated
    //      the type of the child workflow
    | ChildWorkflowExecutionTerminated  of EventId * EventId * WorkflowExecution * WorkflowType
    // ChildWorkflowExecutionTimedOut args:
    //      start child workflow execution initiated event Id
    //      child workflow execution started event Id
    //      the child workflow that timed out
    //      the type of the child workflow
    //      the type of timeout (valid values : StartToClose)
    | ChildWorkflowExecutionTimedOut    of EventId * EventId * WorkflowExecution * WorkflowType * TimeoutType
    // CompleteWorkflowExecutionFailed args:
    //      decision task completed event Id
    //      the cause of the failure
    | CompleteWorkflowExecutionFailed   of EventId * Cause
    // ContinueAsNewWorkflowExecutionFailed args:
    //      decision task completed event Id
    //      the cause of the failure
    | ContinueAsNewWorkflowExecutionFailed      of EventId * Cause
    // DecisionTaskCompleted args:
    //      scheduled event Id
    //      started event Id
    //      user defined context for the workflow execution
    | DecisionTaskCompleted             of EventId * EventId * ExecutionContext option
    // DecisionTaskScheduled args:
    //      task list
    //      start to close timeout
    | DecisionTaskScheduled             of TaskList * Seconds option
    // DecisionTaskStarted args:
    //      scheduled event Id
    //      identity of the worker assigned
    | DecisionTaskStarted               of EventId * Identity option
    // DecisionTaskTimedOut args:
    //      scheduled event Id
    //      started event Id
    //      the type of timeout (valid values : StartToClose)
    | DecisionTaskTimedOut              of EventId * EventId * TimeoutType
    // ExternalWorkflowExecutionCancelRequested args:
    //      request cancel external workflow execution initiated event Id
    //      the external workflow to which the cancellation request was delivered
    | ExternalWorkflowExecutionCancelRequested  of EventId * WorkflowExecution
    // ExternalWorkflowExecutionSignaled args:
    //      signal external workflow execution initiated event Id
    //      the external workflow that the signal was delivered to
    | ExternalWorkflowExecutionSignaled of EventId * WorkflowExecution
    // FailWorkflowExecutionFailed args:
    //      decision task completed event Id
    //      the cause of the failure
    | FailWorkflowExecutionFailed       of EventId * Cause
    // MarkerRecorded args:
    //      decision task completed event Id
    //      name of the marker
    //      details of the marker
    | MarkerRecorded                    of EventId * MarkerName option * Details option
    // RequestCancelActivityTaskFailed args:
    //      activity Id
    //      decision task completed event Id
    //      the cause of the failure to process the decision
    | RequestCancelActivityTaskFailed   of ActivityId * EventId * Cause
    // RequestCancelExternalWorkflowExecutionFailed args:
    //      decision task completed event Id
    //      request cancel external workflow execution initiated event Id
    //      workflow Id that failed
    //      the cause of the failure to process the decision
    //      the run Id of the external workflow execution
    //      optional control data
    | RequestCancelExternalWorkflowExecutionFailed      of EventId * EventId * WorkflowId * Cause * RunId option * Control option
    // RequestCancelExternalWorkflowExecutionInitiated args:
    //      decision task completed event Id
    //      workflow Id to be cancelled
    //      the run Id of the external workflow execution
    //      optional control data
    | RequestCancelExternalWorkflowExecutionInitiated   of EventId * WorkflowId * RunId option * Control option
    // ScheduleActivityTaskFailed args:
    //      activity Id
    //      activity type
    //      decision task completed event Id
    //      the cause of the failure to process the decision
    | ScheduleActivityTaskFailed                of ActivityId * ActivityType * EventId * Cause
    // SignalExternalWorkflowExecutionFailed args:
    //      decision task completed event Id
    //      signal external workflow execution initiated event Id
    //      workflow Id of the external workflow execution that the signal was being delivered to
    //      the cause of the failure to process the decision
    //      the run Id of the external workflow execution
    //      optional control data
    | SignalExternalWorkflowExecutionFailed     of EventId * EventId * WorkflowId * Cause * RunId option
    // SignalExternalWorkflowExecutionInitiated args:
    //      decision task completed event Id
    //      workflow id of the external workflow execution
    //      name of the signal
    //      the run Id of the external workflow execution
    //      input provided to the signal
    //      optional control data
    | SignalExternalWorkflowExecutionInitiated  of EventId * WorkflowId * SignalName * RunId option * Input option * Control option
    // StartChildWorkflowExecutionFailed args:
    //      decision task completed event Id
    //      start child workflow execution initiated event Id
    //      workflow Id of the child workflow execution
    //      workflow type of the child workflow
    //      the cause of the failure to process the decision
    //      optional control data
    | StartChildWorkflowExecutionFailed         of EventId * EventId * WorkflowId * WorkflowType * Cause * Control option
    // StartChildWorkflowExecutionInitiated args:
    //      decision task completed event Id
    //      task list used for the decision task of the child workflow execution
    //      workflow Id of the child workflow execution
    //      workflow type of the child workflow
    //      policy used for the child workflow execution if this execution gets terminated
    //      list of tags associated with the child workflow execution
    //      input provided to the child workflow execution
    //      optional control data
    //      decision task start to close timeout
    //      execution start to close timeout    
    | StartChildWorkflowExecutionInitiated      of EventId * TaskList * WorkflowId * WorkflowType * ChildPolicy * TagList option * Input option * Control option * Seconds option * Seconds option
    // StartTimerFailed args:
    //      decision task completed event Id
    //      timer id
    //      the cause of the failure to process the decision
    | StartTimerFailed                  of EventId * TimerId * Cause
    // TimerCanceled args:
    //      decision task completed event Id
    //      started event Id
    //      timer Id
    | TimerCanceled                     of EventId * EventId * TimerId
    // TimerFired args:
    //      started event Id
    //      timer Id
    | TimerFired                        of EventId * TimerId
    // TimerStarted args:
    //      decision task completed event Id
    //      timer Id
    //      start to fire timeout
    //      optional control data
    | TimerStarted                      of EventId * TimerId * Seconds * Control option
    // WorkflowExecutionCanceled args:
    //      decision task completed event Id
    //      details for the cancellation
    | WorkflowExecutionCanceled         of EventId * Details option
    // WorkflowExecutionCancelRequested args:
    //      request cancel external workflow execution initiated event Id
    //      external workflow execution for which the cancellation was requested
    //      if set, indicates that the request to cancel the workflow execution was automatically generated and specifies the cause
    | WorkflowExecutionCancelRequested  of EventId option * WorkflowExecution option * Cause option
    // WorkflowExecutionCompleted args:
    //      decision task completed event Id
    //      result provided by the workflow
    | WorkflowExecutionCompleted        of EventId * Result option
    // WorkflowExecutionContinuedAsNew args:
    //      decision task completed event Id
    //      task list
    //      workflow type
    //      run Id of the new workflow execution
    //      policy to use for the child workflow executions of the new execution if it is terminated
    //      list of tags associated with the new workflow execution
    //      input provided to new workflow execution
    //      decision task start to close time out
    //      execution start to close time out
    | WorkflowExecutionContinuedAsNew   of EventId * TaskList * WorkflowType * RunId * ChildPolicy * TagList option * Input option * Seconds option * Seconds option
    // WorkflowExecutionFailed args:
    //      decision task completed event Id
    //      details of the failure
    //      descriptive reason for the failure
    | WorkflowExecutionFailed           of EventId * Details option * Reason option
    // WorkflowExecutionSignaled args:
    //      signal external workflow execution initiated event Id
    //      name of the signal
    //      workflow execution that sent the signal
    //      input provided with the signal
    | WorkflowExecutionSignaled         of EventId * SignalName * WorkflowExecution option * Input option
    // WorkflowExecutionStarted args:
    //      task list
    //      workflow type of this execution
    //      policy to use for the child workflow executions if this workflow execution is terminated
    //      continued execution run Id
    //      parent initiated event Id
    //      source workflow execution that started this workflow execution
    //      list of tags associated with the workflow execution
    //      input provided to the workflow execution
    //      decision task start to close time out
    //      execution start to close time out
    | WorkflowExecutionStarted          of TaskList * WorkflowType * ChildPolicy * RunId option * EventId option * WorkflowExecution option * TagList option * Input option * Seconds option * Seconds option
    // WorkflowExecutionTerminated args:
    //      policy used for the child workflow executions of this workflow execution
    //      details provided for the termination
    //      reason provided for the termination
    //      if set, indicates that the workflow execution was automatically terminated, and specifies the cause
    | WorkflowExecutionTerminated       of ChildPolicy * Details option * Reason option * Cause option
    // WorkflowExecutionTimedOut args:
    //      policy used for the child workflow executions of this workflow execution
    //      the type of timeout (valid values : StartToClose)
    | WorkflowExecutionTimedOut         of ChildPolicy * TimeoutType
    
    static member op_Explicit (evt : Amazon.SimpleWorkflow.Model.HistoryEvent) = 
        match evt.EventType.Value with
        | "ActivityTaskCanceled"
            -> let attr = evt.ActivityTaskCanceledEventAttributes
               ActivityTaskCanceled(attr.ScheduledEventId, attr.StartedEventId, !attr.Details, Some attr.LatestCancelRequestedEventId)
        | "ActivityTaskCancelRequested" 
            -> let attr = evt.ActivityTaskCancelRequestedEventAttributes
               ActivityTaskCancelRequested(attr.ActivityId, attr.DecisionTaskCompletedEventId)
        | "ActivityTaskCompleted"
            -> let attr = evt.ActivityTaskCompletedEventAttributes
               ActivityTaskCompleted(attr.ScheduledEventId, attr.StartedEventId, !attr.Result)
        | "ActivityTaskFailed"
            -> let attr = evt.ActivityTaskFailedEventAttributes
               ActivityTaskFailed(attr.ScheduledEventId, attr.StartedEventId, !attr.Details, !attr.Reason)
        | "ActivityTaskScheduled"
            -> let attr = evt.ActivityTaskScheduledEventAttributes
               ActivityTaskScheduled(attr.ActivityId, attr.ActivityType, 
                                     attr.DecisionTaskCompletedEventId, attr.TaskList, 
                                     !attr.Control, !attr.Input, 
                                     attr.HeartbeatTimeout       |> secondsOp, 
                                     attr.ScheduleToStartTimeout |> secondsOp, 
                                     attr.StartToCloseTimeout    |> secondsOp, 
                                     attr.ScheduleToCloseTimeout |> secondsOp)
        | "ActivityTaskStarted" 
            -> let attr = evt.ActivityTaskStartedEventAttributes
               ActivityTaskStarted(attr.ScheduledEventId, attr.Identity |> stringOp)
        | "ActivityTaskTimedOut"
            -> let attr = evt.ActivityTaskTimedOutEventAttributes
               ActivityTaskTimedOut(attr.ScheduledEventId, 
                                    attr.StartedEventId, 
                                    attr.TimeoutType.Value |> timeout, 
                                    !attr.Details)
        | "CancelTimerFailed"
            -> let attr = evt.CancelTimerFailedEventAttributes
               CancelTimerFailed(attr.DecisionTaskCompletedEventId, attr.TimerId, attr.Cause.Value)
        | "CancelWorkflowExecutionFailed"
            -> let attr = evt.CancelWorkflowExecutionFailedEventAttributes
               CancelWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.Cause.Value)
        | "ChildWorkflowExecutionCanceled"
            -> let attr = evt.ChildWorkflowExecutionCanceledEventAttributes
               ChildWorkflowExecutionCanceled(attr.InitiatedEventId,  attr.StartedEventId,
                                              attr.WorkflowExecution, attr.WorkflowType,
                                              !attr.Details)
        | "ChildWorkflowExecutionCompleted"
            -> let attr = evt.ChildWorkflowExecutionCompletedEventAttributes
               ChildWorkflowExecutionCompleted(attr.InitiatedEventId,  attr.StartedEventId,
                                               attr.WorkflowExecution, attr.WorkflowType,
                                               !attr.Result)
        | "ChildWorkflowExecutionFailed"
            -> let attr = evt.ChildWorkflowExecutionFailedEventAttributes
               ChildWorkflowExecutionFailed(attr.InitiatedEventId,  attr.StartedEventId,
                                            attr.WorkflowExecution, attr.WorkflowType,
                                            !attr.Details, !attr.Reason)
        | "ChildWorkflowExecutionStarted"
            -> let attr = evt.ChildWorkflowExecutionStartedEventAttributes
               ChildWorkflowExecutionStarted(attr.InitiatedEventId,
                                             attr.WorkflowExecution, attr.WorkflowType)
        | "ChildWorkflowExecutionTerminated"
            -> let attr = evt.ChildWorkflowExecutionTerminatedEventAttributes
               ChildWorkflowExecutionTerminated(attr.InitiatedEventId,  attr.StartedEventId,
                                                attr.WorkflowExecution, attr.WorkflowType)
        | "ChildWorkflowExecutionTimedOut"
            -> let attr = evt.ChildWorkflowExecutionTimedOutEventAttributes
               ChildWorkflowExecutionTimedOut(attr.InitiatedEventId,  attr.StartedEventId,
                                              attr.WorkflowExecution, attr.WorkflowType,
                                              attr.TimeoutType.Value |> timeout)
        | "CompleteWorkflowExecutionFailed"
            -> let attr = evt.CompleteWorkflowExecutionFailedEventAttributes
               CompleteWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.Cause.Value)
        | "ContinueAsNewWorkflowExecutionFailed"
            -> let attr = evt.ContinueAsNewWorkflowExecutionFailedEventAttributes
               ContinueAsNewWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.Cause.Value)
        | "DecisionTaskCompleted"
            -> let attr = evt.DecisionTaskCompletedEventAttributes
               DecisionTaskCompleted(attr.ScheduledEventId, 
                                     attr.StartedEventId, 
                                     !attr.ExecutionContext)
        | "DecisionTaskScheduled" 
            -> let attr = evt.DecisionTaskScheduledEventAttributes
               DecisionTaskScheduled(attr.TaskList, attr.StartToCloseTimeout |> secondsOp)
        | "DecisionTaskStarted"
            -> let attr = evt.DecisionTaskStartedEventAttributes
               DecisionTaskStarted(attr.ScheduledEventId, !attr.Identity)
        | "DecisionTaskTimedOut"
            -> let attr = evt.DecisionTaskTimedOutEventAttributes
               DecisionTaskTimedOut(attr.ScheduledEventId, 
                                    attr.StartedEventId, 
                                    attr.TimeoutType.Value |> timeout)
        | "ExternalWorkflowExecutionCancelRequested"
            -> let attr = evt.ExternalWorkflowExecutionCancelRequestedEventAttributes
               ExternalWorkflowExecutionCancelRequested(attr.InitiatedEventId, attr.WorkflowExecution)
        | "ExternalWorkflowExecutionSignaled"
            -> let attr = evt.ExternalWorkflowExecutionSignaledEventAttributes
               ExternalWorkflowExecutionSignaled(attr.InitiatedEventId, attr.WorkflowExecution)
        | "FailWorkflowExecutionFailed"
            -> let attr = evt.FailWorkflowExecutionFailedEventAttributes
               FailWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.Cause.Value)
        | "MarkerRecorded"
            -> let attr = evt.MarkerRecordedEventAttributes
               MarkerRecorded(attr.DecisionTaskCompletedEventId, !attr.MarkerName, !attr.Details)
        | "RequestCancelActivityTaskFailed"
            -> let attr = evt.RequestCancelActivityTaskFailedEventAttributes
               RequestCancelActivityTaskFailed(attr.ActivityId, attr.DecisionTaskCompletedEventId, attr.Cause.Value)
        | "RequestCancelExternalWorkflowExecutionFailed"
            -> let attr = evt.RequestCancelExternalWorkflowExecutionFailedEventAttributes
               RequestCancelExternalWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.InitiatedEventId,
                                                            attr.WorkflowId, attr.Cause.Value, 
                                                            !attr.RunId, !attr.Control)
        | "RequestCancelExternalWorkflowExecutionInitiated"
            -> let attr = evt.RequestCancelExternalWorkflowExecutionInitiatedEventAttributes
               RequestCancelExternalWorkflowExecutionInitiated(attr.DecisionTaskCompletedEventId, attr.WorkflowId,
                                                               !attr.RunId, !attr.Control)
        | "ScheduleActivityTaskFailed"
            -> let attr = evt.ScheduleActivityTaskFailedEventAttributes
               ScheduleActivityTaskFailed(attr.ActivityId, attr.ActivityType, 
                                          attr.DecisionTaskCompletedEventId, attr.Cause.Value)
        | "SignalExternalWorkflowExecutionFailed"
            -> let attr = evt.SignalExternalWorkflowExecutionFailedEventAttributes
               SignalExternalWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.InitiatedEventId,
                                                     attr.WorkflowId, attr.Cause.Value,
                                                     !attr.RunId)
        | "SignalExternalWorkflowExecutionInitiated"
            -> let attr = evt.SignalExternalWorkflowExecutionInitiatedEventAttributes
               SignalExternalWorkflowExecutionInitiated(attr.DecisionTaskCompletedEventId,
                                                        attr.WorkflowId, attr.SignalName,
                                                        !attr.RunId, !attr.Input, !attr.Control)
        | "StartChildWorkflowExecutionFailed"
            -> let attr = evt.StartChildWorkflowExecutionFailedEventAttributes
               StartChildWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.InitiatedEventId,
                                                 attr.WorkflowId, attr.WorkflowType, attr.Cause.Value,
                                                 !attr.Control)
        | "StartChildWorkflowExecutionInitiated"
            -> let attr = evt.StartChildWorkflowExecutionInitiatedEventAttributes
               StartChildWorkflowExecutionInitiated(attr.DecisionTaskCompletedEventId, attr.TaskList,
                                                    attr.WorkflowId, attr.WorkflowType, 
                                                    attr.ChildPolicy.Value              |> childPolicy,
                                                    attr.TagList                        |> tagListOp, 
                                                    !attr.Input, !attr.Control,
                                                    attr.TaskStartToCloseTimeout        |> secondsOp,
                                                    attr.ExecutionStartToCloseTimeout   |> secondsOp)
        | "StartTimerFailed"
            -> let attr = evt.StartTimerFailedEventAttributes
               StartTimerFailed(attr.DecisionTaskCompletedEventId, attr.TimerId, attr.Cause.Value)
        | "TimerCanceled"
            -> let attr = evt.TimerCanceledEventAttributes
               TimerCanceled(attr.DecisionTaskCompletedEventId, attr.StartedEventId, attr.TimerId)
        | "TimerFired"
            -> let attr = evt.TimerFiredEventAttributes
               TimerFired(attr.StartedEventId, attr.TimerId)
        | "TimerStarted"
            -> let attr = evt.TimerStartedEventAttributes
               TimerStarted(attr.DecisionTaskCompletedEventId, attr.TimerId, 
                            attr.StartToFireTimeout |> seconds,
                            !attr.Control)
        | "WorkflowExecutionCanceled"
            -> let attr = evt.WorkflowExecutionCanceledEventAttributes
               WorkflowExecutionCanceled(attr.DecisionTaskCompletedEventId, !attr.Details)
        | "WorkflowExecutionCancelRequested"
            -> let attr = evt.WorkflowExecutionCancelRequestedEventAttributes
               WorkflowExecutionCancelRequested(attr.ExternalInitiatedEventId |> eventIdOp,
                                                !?attr.ExternalWorkflowExecution,
                                                !attr.Cause.Value)
        | "WorkflowExecutionCompleted"
            -> let attr = evt.WorkflowExecutionCompletedEventAttributes
               WorkflowExecutionCompleted(attr.DecisionTaskCompletedEventId, !attr.Result)
        | "WorkflowExecutionContinuedAsNew"
            -> let attr = evt.WorkflowExecutionContinuedAsNewEventAttributes
               WorkflowExecutionContinuedAsNew(attr.DecisionTaskCompletedEventId, attr.TaskList,
                                               attr.WorkflowType, attr.NewExecutionRunId, 
                                               attr.ChildPolicy.Value            |> childPolicy,
                                               attr.TagList                      |> tagListOp,
                                               !attr.Input, 
                                               attr.TaskStartToCloseTimeout      |> secondsOp, 
                                               attr.ExecutionStartToCloseTimeout |> secondsOp)
        | "WorkflowExecutionFailed"
            -> let attr = evt.WorkflowExecutionFailedEventAttributes
               WorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, !attr.Details, !attr.Reason)
        | "WorkflowExecutionSignaled"
            -> let attr = evt.WorkflowExecutionSignaledEventAttributes
               WorkflowExecutionSignaled(attr.ExternalInitiatedEventId, attr.SignalName,
                                         !?attr.ExternalWorkflowExecution,
                                         !attr.Input)
        | "WorkflowExecutionStarted"
            -> let attr = evt.WorkflowExecutionStartedEventAttributes
               WorkflowExecutionStarted(attr.TaskList, attr.WorkflowType, 
                                        attr.ChildPolicy.Value            |> childPolicy,
                                        !attr.ContinuedExecutionRunId,
                                        attr.ParentInitiatedEventId       |> eventIdOp,
                                        !?attr.ParentWorkflowExecution,
                                        attr.TagList |> tagListOp,
                                        !attr.Input,
                                        attr.TaskStartToCloseTimeout      |> secondsOp,
                                        attr.ExecutionStartToCloseTimeout |> secondsOp)
        | "WorkflowExecutionTerminated"
            -> let attr = evt.WorkflowExecutionTerminatedEventAttributes
               WorkflowExecutionTerminated(attr.ChildPolicy.Value |> childPolicy,
                                           !attr.Details, !attr.Reason, !attr.Cause.Value)
        | "WorkflowExecutionTimedOut"
            -> let attr = evt.WorkflowExecutionTimedOutEventAttributes
               WorkflowExecutionTimedOut(attr.ChildPolicy.Value |> childPolicy, attr.TimeoutType.Value |> timeout)
        | str -> raise <| UnknownEventType(str)

type HistoryEvent =
    {
        EventId     : EventId
        Timestamp   : DateTime
        EventType   : EventType
    }
    
    static member op_Explicit (evt : Amazon.SimpleWorkflow.Model.HistoryEvent) = 
        {
            EventId     = evt.EventId
            Timestamp   = evt.EventTimestamp
            EventType   = EventType.op_Explicit evt
        }

type DecisionTask (task   : Amazon.SimpleWorkflow.Model.DecisionTask, 
                   client : Amazon.SimpleWorkflow.AmazonSimpleWorkflowClient,
                   domain,
                   tasklist) =
    let nextPageToken = ref task.NextPageToken

    // returns more events for this task if possible
    let getMoreEvts () = 
        if String.IsNullOrWhiteSpace(nextPageToken.Value) 
        then [||] :> Amazon.SimpleWorkflow.Model.HistoryEvent seq
        else
            // poll for decision task again with the next page token to get more events
            let req = PollForDecisionTaskRequest(Domain         = domain, 
                                                 ReverseOrder   = true,
                                                 NextPageToken  = nextPageToken.Value,
                                                 TaskList       = new TaskList(Name = tasklist))
            let res = client.PollForDecisionTask(req)
            nextPageToken := res.DecisionTask.NextPageToken
            res.DecisionTask.Events :> Amazon.SimpleWorkflow.Model.HistoryEvent seq

    // the sequence of raw history events (from the AWSSDK)
    let rawEvents = seq {
        yield! task.Events
        while not <| String.IsNullOrWhiteSpace(nextPageToken.Value) do
            yield! getMoreEvts()
    }
                    
    member this.Domain                  = domain
    member this.Events                  = rawEvents |> Seq.map HistoryEvent.op_Explicit |> Seq.cache
    member this.StartedEventId          = task.StartedEventId
    member this.PreviousStartedEventId  = task.PreviousStartedEventId
    member this.TaskToken               = task.TaskToken
    member this.WorkflowExecution       = task.WorkflowExecution
    member this.WorkflowType            = task.WorkflowType