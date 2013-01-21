namespace Amazon.SimpleWorkflow.Extensions

open System.Collections.Generic

open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Model

[<Measure>]
type sec      // seconds

// #region type alias

type ActivityId     = string
type Control        = string
type Description    = string
type Details        = string
type Domain         = string
type Input          = string
type MarkerName     = string
type Name           = string
type Reason         = string
type Result         = string
type RunId          = string
type Seconds        = int<sec>
type SignalName     = string
type TagList        = string[]
type TaskList       = string
type TimerId        = string
type Version        = string
type WorkflowId     = string
type WorkflowType   = Name * Version

// #endregion

[<AutoOpen>]
module Helpers =
    let inline str x = x.ToString()

    // operator which only executes f with the value of x if x is not None
    let inline (?->) (x : 'a option) (f : 'a -> 'b) = match x with | Some x' -> f x' |> ignore | _ -> ()

    // reverse operator of the above
    let inline (<-?) (f : 'a -> 'b) (x : 'a option) = x ?-> f

type ChildPolicy =
    | Terminate
    | RequestCancel
    | Abandon

    override this.ToString() =
        match this with
        | Terminate     -> "TERMINATE"
        | RequestCancel -> "REQUEST_CANCEL"
        | Abandon       -> "ABANDON"

/// The different types of decisions
/// see http://docs.aws.amazon.com/amazonswf/latest/apireference/API_Decision.html
type Decisions =   
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
    
    /// Returns a corresponding instance of Decision
    member this.ToDecision () =
        let decision = new Decision(DecisionType = this.DecisionType)
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
               taskList    ?-> (toTaskList >> attrs.WithTaskList)
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
               
               taskList             ?-> (toTaskList >> attrs.WithTaskList)
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
                            .WithWorkflowType(toWorkflowType workflowType)
               
               childPolicy ?-> (str >> attrs.WithChildPolicy)
               taskList    ?-> (toTaskList >> attrs.WithTaskList)
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