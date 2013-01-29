namespace Amazon.SimpleWorkflow.Extensions

open System
open System.Threading
open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Model

[<AutoOpen>]
module AsyncExtensions =
    type AmazonSimpleWorkflowClient with
        member this.PollForActivityTaskAsync (req : PollForActivityTaskRequest) = Async.FromBeginEnd(req, this.BeginPollForActivityTask, this.EndPollForActivityTask)
        member this.PollForDecisionTaskAsync (req : PollForDecisionTaskRequest) = Async.FromBeginEnd(req, this.BeginPollForDecisionTask, this.EndPollForDecisionTask)

        member this.RespondActivityTaskCanceledAsync (req : RespondActivityTaskCanceledRequest)     = Async.FromBeginEnd(req, this.BeginRespondActivityTaskCanceled, this.EndRespondActivityTaskCanceled)
        member this.RespondActivityTaskCompletedAsync (req : RespondActivityTaskCompletedRequest)   = Async.FromBeginEnd(req, this.BeginRespondActivityTaskCompleted, this.EndRespondActivityTaskCompleted)
        member this.RespondActivityTaskFailedAsync (req : RespondActivityTaskFailedRequest)         = Async.FromBeginEnd(req, this.BeginRespondActivityTaskFailed, this.EndRespondActivityTaskFailed)
        member this.RespondDecisionTaskCompletedAsync (req : RespondDecisionTaskCompletedRequest)   = Async.FromBeginEnd(req, this.BeginRespondDecisionTaskCompleted, this.EndRespondDecisionTaskCompleted)

        member this.DescribeActivityTypeAsync (req : DescribeActivityTypeRequest)                   = Async.FromBeginEnd(req, this.BeginDescribeActivityType, this.EndDescribeActivityType)
        member this.DescribeDomainAsync (req : DescribeDomainRequest)                               = Async.FromBeginEnd(req, this.BeginDescribeDomain, this.EndDescribeDomain)
        member this.DescribeWorkflowExecutionAsync (req : DescribeWorkflowExecutionRequest)         = Async.FromBeginEnd(req, this.BeginDescribeWorkflowExecution, this.EndDescribeWorkflowExecution)
        member this.DescribeWorkflowTypeAsync (req : DescribeWorkflowTypeRequest)                   = Async.FromBeginEnd(req, this.BeginDescribeWorkflowType, this.EndDescribeWorkflowType)

        member this.GetWorkflowExecutionHistoryAsync (req : GetWorkflowExecutionHistoryRequest)     = Async.FromBeginEnd(req, this.BeginGetWorkflowExecutionHistory, this.EndGetWorkflowExecutionHistory)

        member this.ListActivityTypesAsync (req : ListActivityTypesRequest)                         = Async.FromBeginEnd(req, this.BeginListActivityTypes, this.EndListActivityTypes)
        member this.ListClosedWorkflowExecutionsAsync (req : ListClosedWorkflowExecutionsRequest)   = Async.FromBeginEnd(req, this.BeginListClosedWorkflowExecutions, this.EndListClosedWorkflowExecutions)
        member this.ListDomainsAsync (req : ListDomainsRequest)                                     = Async.FromBeginEnd(req, this.BeginListDomains, this.EndListDomains)
        member this.ListOpenWorkflowExecutionsAsync (req : ListOpenWorkflowExecutionsRequest)       = Async.FromBeginEnd(req, this.BeginListOpenWorkflowExecutions, this.EndListOpenWorkflowExecutions)
        member this.ListWorkflowTypesAsync (req : ListWorkflowTypesRequest)                         = Async.FromBeginEnd(req, this.BeginListWorkflowTypes, this.EndListWorkflowTypes)        

        member this.RecordActivityTaskHeartbeatAsync (req : RecordActivityTaskHeartbeatRequest)     = Async.FromBeginEnd(req, this.BeginRecordActivityTaskHeartbeat, this.EndRecordActivityTaskHeartbeat)

        member this.CountClosedWorkflowExecutionsAsync (req : CountClosedWorkflowExecutionsRequest) = Async.FromBeginEnd(req, this.BeginCountClosedWorkflowExecutions, this.EndCountClosedWorkflowExecutions)
        member this.CountOpenWorkflowExecutionsAsync (req : CountOpenWorkflowExecutionsRequest)     = Async.FromBeginEnd(req, this.BeginCountOpenWorkflowExecutions, this.EndCountOpenWorkflowExecutions)
        member this.CountPendingActivityTasksAsync (req : CountPendingActivityTasksRequest)         = Async.FromBeginEnd(req, this.BeginCountPendingActivityTasks, this.EndCountPendingActivityTasks)
        member this.CountPendingDecisionTasksAsync (req : CountPendingDecisionTasksRequest)         = Async.FromBeginEnd(req, this.BeginCountPendingDecisionTasks, this.EndCountPendingDecisionTasks)

        member this.RegisterActivityTypeAsync (req : RegisterActivityTypeRequest)   = Async.FromBeginEnd(req, this.BeginRegisterActivityType, this.EndRegisterActivityType)
        member this.RegisterDomainAsync (req : RegisterDomainRequest)               = Async.FromBeginEnd(req, this.BeginRegisterDomain, this.EndRegisterDomain)
        member this.RegisterWorkflowTypeAsync (req : RegisterWorkflowTypeRequest)   = Async.FromBeginEnd(req, this.BeginRegisterWorkflowType, this.EndRegisterWorkflowType)        

        member this.RequestCancelWorkflowExecutionAsync (req : RequestCancelWorkflowExecutionRequest)   = Async.FromBeginEnd(req, this.BeginRequestCancelWorkflowExecution, this.EndRequestCancelWorkflowExecution)

        member this.SignalWorkflowExecutionAsync (req : SignalWorkflowExecutionRequest)       = Async.FromBeginEnd(req, this.BeginSignalWorkflowExecution, this.EndSignalWorkflowExecution)
        member this.StartWorkflowExecutionAsync (req : StartWorkflowExecutionRequest)         = Async.FromBeginEnd(req, this.BeginStartWorkflowExecution, this.EndStartWorkflowExecution)
        member this.TerminateWorkflowExecutionAsync (req : TerminateWorkflowExecutionRequest) = Async.FromBeginEnd(req, this.BeginTerminateWorkflowExecution, this.EndTerminateWorkflowExecution)