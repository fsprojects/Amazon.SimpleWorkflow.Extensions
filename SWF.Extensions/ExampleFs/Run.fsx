#r "bin/Debug/AWSSDK.dll"
#r "bin/Debug/SWF.Extensions.Core.dll"

#load "BasicExamples.fs"

open Amazon
open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Extensions

let awsKey      = "PUT_YOUR_AWS_KEY_HERE"
let awsSecret   = "PUT_YOUR_AWS_SECRET_HERE"

let start (workflow : IWorkflow) =
    workflow.Start(awsKey, awsSecret, Amazon.RegionEndpoint.USEast1)

// uncomment the example you want to run to test it out
//start BasicExamples.helloWorldWorkflow
//start BasicExamples.echoWorkflow
//start BasicExamples.genericActivityWorkflow
//start BasicExamples.simplePipelineWorkflow
//start BasicExamples.withChildWorkflow
//start BasicExamples.failedWorkflowWithActivity
//start BasicExamples.failedWorkflowWithChildWorkflow
//start BasicExamples.parallelActivities
//start BasicExamples.parallelSchedulables
//start BasicExamples.failedParallelSchedulables