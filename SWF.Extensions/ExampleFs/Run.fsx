#r "bin/Debug/AWSSDK.dll"
#r "bin/Debug/SWF.Extensions.Core.dll"

#load "BasicExamples.fs"

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Extensions
open Amazon.SimpleWorkflow.Model

let awsKey      = "AKIAJWLLBHKGW7TOXTFQ"
let awsSecret   = "xS+bLuqPqy14R3GTOYvzQ2MmAW+hZiLiFPrtvrku"
let client = new AmazonSimpleWorkflowClient(awsKey, awsSecret)

// uncomment the example you want to run to test it out
BasicExamples.helloWorldWorkflow.Start(client)
//BasicExamples.echoWorkflow.Start(client)
//BasicExamples.genericActivityWorkflow.Start(client)
//BasicExamples.simplePipelineWorkflow.Start(client)
//BasicExamples.withChildWorkflow.Start(client)