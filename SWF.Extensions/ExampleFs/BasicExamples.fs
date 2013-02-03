module BasicExamples

open System

open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Extensions.Model

// #region Hello World example

let sayHelloWorld _ = printfn "Hello World!"; ""

/// workflow which has only one activity - to print hello world when received an activity task
let helloWorldWorkflow =
    Workflow(domain = "theburningmonk.com", name = "hello_world", 
             description = "simple 'hello world' example", 
             version = "1",
             identity = "Phantom") // you can optionally set the identity of your decision and activity workers
    ++> Activity("say_hello_world", "say 'hello world'", sayHelloWorld,
                 taskHeartbeatTimeout       = 60, 
                 taskScheduleToStartTimeout = 10,
                 taskStartToCloseTimeout    = 10, 
                 taskScheduleToCloseTimeout = 20)

// #endregion

// #region Echo example

let echo str = printfn "%s" str; str

let echoWorkflow =
    Workflow(domain = "theburningmonk.com", name = "echo", 
             description = "simple echo example", 
             version = "1")
    ++> Activity("echo", "echo input", echo,
                 taskHeartbeatTimeout       = 60, 
                 taskScheduleToStartTimeout = 10,
                 taskStartToCloseTimeout    = 10, 
                 taskScheduleToCloseTimeout = 20)

// #endregion

// #region Generic Activity example

let sum (arr : int[]) = arr |> Array.sum |> double

let genericActivityWorkflow =
    Workflow(domain = "theburningmonk.com", name = "generic_activity", 
             description = "simple generic activity example", 
             version = "1")
    ++> Activity<int[], double>("sum", "sum int array into a double", sum,
                                taskHeartbeatTimeout       = 60, 
                                taskScheduleToStartTimeout = 10,
                                taskStartToCloseTimeout    = 10, 
                                taskScheduleToCloseTimeout = 20)

// #endregion

// #region Simple Pipeline example 
// i.e. a chain of activities where result from the previous activity is passed on
// as the input to the next activity

// the most boring conversation ever between me and another person.. ("@")/  \(*@*)
let greet me you = printfn "%s: hello %s!" me you; you
let bye me you = printfn "%s: good bye, %s!" me you; me

let simplePipelineWorkflow =
    Workflow(domain = "theburningmonk.com", name = "simple_pipeline", 
             description = "simple pipeline example", 
             version = "1")
    ++> Activity("greet", "say hello", greet "Yan",
                 taskHeartbeatTimeout       = 60, 
                 taskScheduleToStartTimeout = 10,
                 taskStartToCloseTimeout    = 10, 
                 taskScheduleToCloseTimeout = 20)
    ++> Activity("bye", "say good bye", bye "Yan",
                 taskHeartbeatTimeout       = 60, 
                 taskScheduleToStartTimeout = 10,
                 taskStartToCloseTimeout    = 10, 
                 taskScheduleToCloseTimeout = 20)

// #endregion

// #region Child Workflow example
// i.e. kick off a child workflow from the main workflow, passing result from the
// last activity to the child workflow as input, and taking the result of the
// child workflow as input to the next activity

// sings the first part of the song and return the second part as result
let sing name = printfn "Old %s had a farm" name; "EE-I-EE-I-O"

// unlike the main workflow, child workflows MUST specify the timeouts and child
// policy on the workflow definition itself
let childWorkflow = 
    Workflow(domain = "theburningmonk.com", name = "sing_along", 
             description = "child workflow to start a song", 
             version = "1",
             execStartToCloseTimeout = 60, 
             taskStartToCloseTimeout = 30,
             childPolicy = ChildPolicy.Terminate)
    ++> Activity("sing", "sing a song", sing,
                 taskHeartbeatTimeout       = 60, 
                 taskScheduleToStartTimeout = 10,
                 taskStartToCloseTimeout    = 10, 
                 taskScheduleToCloseTimeout = 20)

// this is the main workflow which
let withChildWorkflow =
    Workflow(domain = "theburningmonk.com", name = "with_child_workflow", 
             description = "workflow which starts a child workflow in the middle", 
             version = "1")
    ++> Activity("greet", "say hello", greet "MacDonald",
                 taskHeartbeatTimeout       = 60, 
                 taskScheduleToStartTimeout = 10,
                 taskStartToCloseTimeout    = 10, 
                 taskScheduleToCloseTimeout = 20)
    ++> Activity("bye", "say good bye", bye "MacDonald",
                 taskHeartbeatTimeout       = 60, 
                 taskScheduleToStartTimeout = 10,
                 taskStartToCloseTimeout    = 10, 
                 taskScheduleToCloseTimeout = 20)
    ++> childWorkflow
    ++> Activity("echo", "echo the last part of the song", echo,
                 taskHeartbeatTimeout       = 60, 
                 taskScheduleToStartTimeout = 10,
                 taskStartToCloseTimeout    = 10, 
                 taskScheduleToCloseTimeout = 20)

// #endregion

// #region Failed Workflow (Activity) example
// you can specify a max number of attempts for each activity, the activity will be retried up
// to that many attempts (e.g. max 3 attempts = 1 attempt + 2 retries), if the activity failed
// or timed out on the last retry then it'll be failed and the whole workflow will be failed

let alwaysFail _ = failwith "oops"

let failedWorkflowWithActivity = 
    Workflow(domain = "theburningmonk.com", name = "failed_workflow_with_activity", 
             description = "this workflow will fail because of its activity", 
             version = "1",
             execStartToCloseTimeout = 60, 
             taskStartToCloseTimeout = 30,
             childPolicy = ChildPolicy.Terminate)
    ++> Activity("always_fail", "this activity will always fail", alwaysFail,
                 taskHeartbeatTimeout       = 60, 
                 taskScheduleToStartTimeout = 10,
                 taskStartToCloseTimeout    = 10, 
                 taskScheduleToCloseTimeout = 20,
                 maxAttempts = 3)   // max 3 attempts, and fail the workflow after that

// #endregion

// #region Failed Workflow (ChildWorkflow) example
// the same retry mechanism can be applied to child workflows too

// unlike the main workflow, child workflows MUST specify the timeouts and child
// policy on the workflow definition itself
let alwaysFailChildWorkflow = 
    Workflow(domain = "theburningmonk.com", name = "failed_child_workflow", 
             description = "this child workflow will always fail", 
             version = "1",
             execStartToCloseTimeout = 60, 
             taskStartToCloseTimeout = 30,
             childPolicy = ChildPolicy.Terminate,
             maxAttempts = 3)
    ++> Activity("always_fail", "this activity will always fail", alwaysFail,
                 taskHeartbeatTimeout       = 60, 
                 taskScheduleToStartTimeout = 10,
                 taskStartToCloseTimeout    = 10, 
                 taskScheduleToCloseTimeout = 20)

let failedWorkflowWithChildWorkflow = 
    Workflow(domain = "theburningmonk.com", name = "failed_workflow_with_child_workflow", 
             description = "this workflow will fail because of its child workflow", 
             version = "1",
             execStartToCloseTimeout = 60, 
             taskStartToCloseTimeout = 30,
             childPolicy = ChildPolicy.Terminate)
    ++> alwaysFailChildWorkflow

// #endregion