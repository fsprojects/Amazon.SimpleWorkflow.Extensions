module SWF.Extensions.Core.Model

open System
open System.Collections.Generic

open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Model

// #region type alias

type ActivityId         = string
type Cause              = string
type Control            = string
type Description        = string
type Details            = string
type Domain             = string
type EventId            = int64
type ExecutionContext   = string
type Input              = string
type MarkerName         = string
type Name               = string
type Reason             = string
type Result             = string
type RunId              = string
type Seconds            = int
type SignalName         = string
type SwfDecision        = Amazon.SimpleWorkflow.Model.Decision
type TagList            = string[]
type TimerId            = string
type Version            = string
type WorkerId           = string
type WorkflowId         = string

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

    static member op_Explicit = function
        | "START_TO_CLOSE"      -> StartToClose
        | "SCHEDULE_TO_START"   -> ScheduleToStart
        | "SCHEDULE_TO_CLOSE"   -> ScheduleToClose
        | "HEARTBEAT"           -> Heartbeat
        | str                   -> raise <| UnknownTimeoutType str

// #region Custom Operators and Convertor functions

// operator for converting a string to a TaskList (from the AWSSDK)
//let inline (~%) name  = new Amazon.SimpleWorkflow.Model.TaskList(Name = name)

let inline str x = x.ToString()

// operator which only executes f with the value of x if x is not None
let inline (?->) (x : 'a option) (f : 'a -> 'b) = match x with | Some x' -> f x' |> ignore | _ -> ()

// reverse operator of the above
let inline (<-?) (f : 'a -> 'b) (x : 'a option) = x ?-> f

// helper functions to convert from one type to an option type (used in the conversion from HistoryEvent to EventType
let inline stringOp x  = if String.IsNullOrWhiteSpace x then None else Some x
let inline taskListOp (x : Amazon.SimpleWorkflow.Model.TaskList) = match x with | null -> None | _ -> stringOp x.Name
let inline (!) (str : string) = stringOp str

let inline asOption x = match x with | null -> None | _ -> Some x
let inline (!?) x = asOption x

let inline eventIdOp x = (int64 >> function | 0L -> None | x' -> Some x') x

let inline secondsOp x = (int >> function | 0 -> None | x' -> Some x') x
let inline seconds x = int x
let inline timeout x = TimeoutType.op_Explicit x
let inline childPolicy x = ChildPolicy.op_Explicit x

let inline tagListOp (lst : List<string>) = match lst with 
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
        | CancelTimer _                             -> "CancelTimer"
        | CancelWorkflowExecution _                 -> "CancelWorkflowExecution"
        | CompleteWorkflowExecution _               -> "CompleteWorkflowExecution"
        | ContinueAsNewWorkflowExecution _          -> "ContinueAsNewWorkflowExecution"
        | FailWorkflowExecution _                   -> "FailWorkflowExecution"
        | RecordMarker _                            -> "RecordMarker"
        | RequestCancelActivityTask _               -> "RequestCancelActivityTask"
        | RequestCancelExternalWorkflowExecution _  -> "RequestCancelExternalWorkflowExecution"
        | ScheduleActivityTask _                    -> "ScheduleActivityTask"
        | SignalExternalWorkflowExecution _         -> "SignalExternalWorkflowExecution"
        | StartChildWorkflowExecution _             -> "StartChildWorkflowExecution"
        | StartTimer _                              -> "StartTimer"
    
    /// Returns a corresponding instance of Decision as defined in the AWSSDK
    member this.ToSwfDecision () =
        let decision = new SwfDecision(DecisionType = this.DecisionType)
        match this with
        | CancelTimer timerId
            -> let attrs = CancelTimerDecisionAttributes().WithTimerId(timerId)
               decision.WithCancelTimerDecisionAttributes(attrs)
        | CancelWorkflowExecution details
            -> let attrs = CancelWorkflowExecutionDecisionAttributes()
               attrs.WithDetails <-? details
               decision.WithCancelWorkflowExecutionDecisionAttributes(attrs)
        | CompleteWorkflowExecution result
            -> let attrs = CompleteWorkflowExecutionDecisionAttributes()
               attrs.WithResult <-? result
               decision.WithCompleteWorkflowExecutionDecisionAttributes(attrs)
        | ContinueAsNewWorkflowExecution(childPolicy, taskList, input, tagList, execTimeout, taskTimeout, version)
            -> let attrs = ContinueAsNewWorkflowExecutionDecisionAttributes()

               childPolicy ?-> (str >> attrs.WithChildPolicy)
               taskList    ?-> attrs.WithTaskList
               input       ?-> attrs.WithInput
               tagList     ?-> (fun lst -> attrs.WithTagList(new List<string>(lst)))
               execTimeout ?-> (str >> attrs.WithExecutionStartToCloseTimeout)
               taskTimeout ?-> (str >> attrs.WithTaskStartToCloseTimeout)
               version     ?-> attrs.WithWorkflowTypeVersion

               decision.WithContinueAsNewWorkflowExecutionDecisionAttributes(attrs)
        | FailWorkflowExecution(details, reason)
            -> let attrs = FailWorkflowExecutionDecisionAttributes()
               attrs.WithDetails <-? details
               attrs.WithReason  <-? reason
               decision.WithFailWorkflowExecutionDecisionAttributes(attrs)
        | RecordMarker(markerName, details)
            -> let attrs = RecordMarkerDecisionAttributes().WithMarkerName(markerName)
               attrs.WithDetails <-? details
               decision.WithRecordMarkerDecisionAttributes(attrs)
        | RequestCancelActivityTask activityId
            -> let attrs = RequestCancelActivityTaskDecisionAttributes(ActivityId = activityId)
               decision.WithRequestCancelActivityTaskDecisionAttributes(attrs)
        | RequestCancelExternalWorkflowExecution(workflowId, runId, control) 
            -> let attrs = RequestCancelExternalWorkflowExecutionDecisionAttributes().WithWorkflowId(workflowId)
               attrs.WithRunId   <-? runId
               attrs.WithControl <-? control
               decision.WithRequestCancelExternalWorkflowExecutionDecisionAttributes(attrs)
        | ScheduleActivityTask(activityId, activityType, taskList, input, heartbeatTimeout, schedToStartTimeout, timeout, schedToCloseTimeout, control)
            -> let attrs = ScheduleActivityTaskDecisionAttributes()
                            .WithActivityId(activityId)
                            .WithActivityType(activityType)
               
               taskList             ?-> attrs.WithTaskList
               input                ?-> attrs.WithInput
               heartbeatTimeout     ?-> (str >> attrs.WithHeartbeatTimeout)
               schedToStartTimeout  ?-> (str >> attrs.WithScheduleToStartTimeout)
               timeout              ?-> (str >> attrs.WithStartToCloseTimeout)
               schedToCloseTimeout  ?-> (str >> attrs.WithScheduleToCloseTimeout)
               control              ?-> attrs.WithControl

               decision.WithScheduleActivityTaskDecisionAttributes(attrs)
        | SignalExternalWorkflowExecution(workflowId, signalName, input, runId, control)
            -> let attrs = SignalExternalWorkflowExecutionDecisionAttributes()
                            .WithWorkflowId(workflowId)
                            .WithSignalName(signalName)

               attrs.WithInput   <-? input
               attrs.WithRunId   <-? runId
               attrs.WithControl <-? control

               decision.WithSignalExternalWorkflowExecutionDecisionAttributes(attrs)
        | StartChildWorkflowExecution(workflowId, workflowType, childPolicy, taskList, input, tagList, execTimeout, taskTimeout, control)
            -> let attrs = StartChildWorkflowExecutionDecisionAttributes()
                            .WithWorkflowId(workflowId)
                            .WithWorkflowType(workflowType)
               
               childPolicy ?-> (str >> attrs.WithChildPolicy)
               taskList    ?-> attrs.WithTaskList
               input       ?-> attrs.WithInput
               tagList     ?-> (fun lst -> attrs.WithTagList(new List<string>(lst)))
               execTimeout ?-> (str >> attrs.WithExecutionStartToCloseTimeout)
               taskTimeout ?-> (str >> attrs.WithTaskStartToCloseTimeout)
               control     ?-> attrs.WithControl

               decision.WithStartChildWorkflowExecutionDecisionAttributes(attrs)
        | StartTimer(timerId, startToFireTimeout, control)
            -> let attrs = StartTimerDecisionAttributes()
                            .WithTimerId(timerId)
                            .WithStartToFireTimeout(str startToFireTimeout)
               
               control ?-> attrs.WithControl

               decision.WithStartTimerDecisionAttributes(attrs)

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
    | ActivityTaskStarted               of EventId * WorkerId option
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
    //      id of the worker assigned
    | DecisionTaskStarted               of EventId * WorkerId option
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
        match evt.EventType with
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
               ActivityTaskTimedOut(attr.ScheduledEventId, attr.StartedEventId, 
                                    attr.TimeoutType    |> timeout, 
                                    attr.Details        |> stringOp)
        | "CancelTimerFailed"
            -> let attr = evt.CancelTimerFailedEventAttributes
               CancelTimerFailed(attr.DecisionTaskCompletedEventId, attr.TimerId, attr.Cause)
        | "CancelWorkflowExecutionFailed"
            -> let attr = evt.CancelWorkflowExecutionFailedEventAttributes
               CancelWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.Cause)
        | "ChildWorkflowExecutionCanceled"
            -> let attr = evt.ChildWorkflowExecutionCanceledEventAttributes
               ChildWorkflowExecutionCanceled(attr.InitiatedEventId,  attr.StartedEventId,
                                              attr.WorkflowExecution, attr.WorkflowType,
                                              attr.Details  |> stringOp)
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
                                              attr.TimeoutType  |> timeout)
        | "CompleteWorkflowExecutionFailed"
            -> let attr = evt.CompleteWorkflowExecutionFailedEventAttributes
               CompleteWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.Cause)
        | "ContinueAsNewWorkflowExecutionFailed"
            -> let attr = evt.ContinueAsNewWorkflowExecutionFailedEventAttributes
               ContinueAsNewWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.Cause)
        | "DecisionTaskCompleted"
            -> let attr = evt.DecisionTaskCompletedEventAttributes
               DecisionTaskCompleted(attr.ScheduledEventId, attr.StartedEventId, 
                                     !attr.ExecutionContext)
        | "DecisionTaskScheduled" 
            -> let attr = evt.DecisionTaskScheduledEventAttributes
               DecisionTaskScheduled(attr.TaskList, attr.StartToCloseTimeout |> secondsOp)
        | "DecisionTaskStarted"
            -> let attr = evt.DecisionTaskStartedEventAttributes
               DecisionTaskStarted(attr.ScheduledEventId, !attr.Identity)
        | "DecisionTaskTimedOut"
            -> let attr = evt.DecisionTaskTimedOutEventAttributes
               DecisionTaskTimedOut(attr.ScheduledEventId, attr.StartedEventId, 
                                    attr.TimeoutType |> timeout)
        | "ExternalWorkflowExecutionCancelRequested"
            -> let attr = evt.ExternalWorkflowExecutionCancelRequestedEventAttributes
               ExternalWorkflowExecutionCancelRequested(attr.InitiatedEventId, attr.WorkflowExecution)
        | "ExternalWorkflowExecutionSignaled"
            -> let attr = evt.ExternalWorkflowExecutionSignaledEventAttributes
               ExternalWorkflowExecutionSignaled(attr.InitiatedEventId, attr.WorkflowExecution)
        | "FailWorkflowExecutionFailed"
            -> let attr = evt.FailWorkflowExecutionFailedEventAttributes
               FailWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.Cause)
        | "MarkerRecorded"
            -> let attr = evt.MarkerRecordedEventAttributes
               MarkerRecorded(attr.DecisionTaskCompletedEventId, !attr.MarkerName, !attr.Details)
        | "RequestCancelActivityTaskFailed"
            -> let attr = evt.RequestCancelActivityTaskFailedEventAttributes
               RequestCancelActivityTaskFailed(attr.ActivityId, attr.DecisionTaskCompletedEventId, attr.Cause)
        | "RequestCancelExternalWorkflowExecutionFailed"
            -> let attr = evt.RequestCancelExternalWorkflowExecutionFailedEventAttributes
               RequestCancelExternalWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.InitiatedEventId,
                                                            attr.WorkflowId, attr.Cause, 
                                                            !attr.RunId, !attr.Control)
        | "RequestCancelExternalWorkflowExecutionInitiated"
            -> let attr = evt.RequestCancelExternalWorkflowExecutionInitiatedEventAttributes
               RequestCancelExternalWorkflowExecutionInitiated(attr.DecisionTaskCompletedEventId, attr.WorkflowId,
                                                               !attr.RunId, !attr.Control)
        | "ScheduleActivityTaskFailed"
            -> let attr = evt.ScheduleActivityTaskFailedEventAttributes
               ScheduleActivityTaskFailed(attr.ActivityId, attr.ActivityType, 
                                          attr.DecisionTaskCompletedEventId, attr.Cause)
        | "SignalExternalWorkflowExecutionFailed"
            -> let attr = evt.SignalExternalWorkflowExecutionFailedEventAttributes
               SignalExternalWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.InitiatedEventId,
                                                     attr.WorkflowId, attr.Cause,
                                                     !attr.RunId)
        | "SignalExternalWorkflowExecutionInitiated"
            -> let attr = evt.SignalExternalWorkflowExecutionInitiatedEventAttributes
               SignalExternalWorkflowExecutionInitiated(attr.DecisionTaskCompletedEventId,
                                                        attr.WorkflowId, attr.SignalName,
                                                        !attr.RunId, !attr.Input, !attr.Control)
        | "StartChildWorkflowExecutionFailed"
            -> let attr = evt.StartChildWorkflowExecutionFailedEventAttributes
               StartChildWorkflowExecutionFailed(attr.DecisionTaskCompletedEventId, attr.InitiatedEventId,
                                                 attr.WorkflowId, attr.WorkflowType, attr.Cause,
                                                 !attr.Control)
        | "StartChildWorkflowExecutionInitiated"
            -> let attr = evt.StartChildWorkflowExecutionInitiatedEventAttributes
               StartChildWorkflowExecutionInitiated(attr.DecisionTaskCompletedEventId, attr.TaskList,
                                                    attr.WorkflowId, attr.WorkflowType, 
                                                    attr.ChildPolicy    |> childPolicy,
                                                    attr.TagList        |> tagListOp, 
                                                    !attr.Input, !attr.Control,
                                                    attr.TaskStartToCloseTimeout        |> secondsOp,
                                                    attr.ExecutionStartToCloseTimeout   |> secondsOp)
        | "StartTimerFailed"
            -> let attr = evt.StartTimerFailedEventAttributes
               StartTimerFailed(attr.DecisionTaskCompletedEventId, attr.TimerId, attr.Cause)
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
                                                !attr.Cause)
        | "WorkflowExecutionCompleted"
            -> let attr = evt.WorkflowExecutionCompletedEventAttributes
               WorkflowExecutionCompleted(attr.DecisionTaskCompletedEventId, !attr.Result)
        | "WorkflowExecutionContinuedAsNew"
            -> let attr = evt.WorkflowExecutionContinuedAsNewEventAttributes
               WorkflowExecutionContinuedAsNew(attr.DecisionTaskCompletedEventId, attr.TaskList,
                                               attr.WorkflowType, attr.NewExecutionRunId, 
                                               attr.ChildPolicy |> childPolicy,
                                               attr.TagList     |> tagListOp,
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
               WorkflowExecutionStarted(attr.TaskList, attr.WorkflowType, attr.ChildPolicy |> childPolicy,
                                        !attr.ContinuedExecutionRunId,
                                        attr.ParentInitiatedEventId       |> eventIdOp,
                                        !?attr.ParentWorkflowExecution,
                                        attr.TagList |> tagListOp,
                                        !attr.Input,
                                        attr.TaskStartToCloseTimeout      |> secondsOp,
                                        attr.ExecutionStartToCloseTimeout |> secondsOp)
        | "WorkflowExecutionTerminated"
            -> let attr = evt.WorkflowExecutionTerminatedEventAttributes
               WorkflowExecutionTerminated(attr.ChildPolicy |> childPolicy,
                                           !attr.Details, !attr.Reason, !attr.Cause)
        | "WorkflowExecutionTimedOut"
            -> let attr = evt.WorkflowExecutionTimedOutEventAttributes
               WorkflowExecutionTimedOut(attr.ChildPolicy |> childPolicy, attr.TimeoutType |> timeout)
        | str                                    -> raise <| UnknownEventType(str)

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

type DecisionTask (task : Amazon.SimpleWorkflow.Model.DecisionTask) =
    member this.Events                  = task.Events |> Seq.map HistoryEvent.op_Explicit |> Seq.toList
    member this.StartedEventId          = task.StartedEventId
    member this.PreviousStartedEventId  = task.PreviousStartedEventId
    member this.TaskToken               = task.TaskToken
    member this.WorkflowExecution       = task.WorkflowExecution
    member this.WorkflowType            = task.WorkflowType