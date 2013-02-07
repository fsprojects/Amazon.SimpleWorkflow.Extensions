#r "bin/Debug/AWSSDK.dll"
#r "bin/Debug/SWF.Extensions.Core.dll"

#load "BasicExamples.fs"

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Extensions

let awsKey      = "PUT_YOUR_AWS_KEY_HERE"
let awsSecret   = "PUT_YOUR_AWS_SECRET_HERE"
let client = new AmazonSimpleWorkflowClient(awsKey, awsSecret)

// uncomment the example you want to run to test it out
//BasicExamples.helloWorldWorkflow.Start(client)
//BasicExamples.echoWorkflow.Start(client)
//BasicExamples.genericActivityWorkflow.Start(client)
//BasicExamples.simplePipelineWorkflow.Start(client)
//BasicExamples.withChildWorkflow.Start(client)
//BasicExamples.failedWorkflowWithActivity.Start(client)
//BasicExamples.failedWorkflowWithChildWorkflow.Start(client)
//BasicExamples.parallelActivities.Start(client)