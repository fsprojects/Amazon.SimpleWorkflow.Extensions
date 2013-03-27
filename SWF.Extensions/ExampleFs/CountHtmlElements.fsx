﻿#r "bin/Release/AWSSDK.dll"
#r "bin/Release/SWF.Extensions.Core.dll"

open Amazon.SimpleWorkflow
open Amazon.SimpleWorkflow.Extensions

open System.Collections.Generic
open System.Net

let echo str = printfn "%s" str; str

// a function to count the number of occurances of a pattern inside the HTML returned
// by the specified URL address
let countMatches (pattern : string) (address : string) =
    let webClient = new WebClient()
    let html = webClient.DownloadString address

    seq { 0..html.Length - pattern.Length }
    |> Seq.map (fun i -> html.Substring(i, pattern.Length))
    |> Seq.filter ((=) pattern)
    |> Seq.length

let echoActivity = Activity(
                        "echo", "echo input", echo,
                        taskHeartbeatTimeout       = 60, 
                        taskScheduleToStartTimeout = 10,
                        taskStartToCloseTimeout    = 10, 
                        taskScheduleToCloseTimeout = 20)

let countDivs = Activity<string, int>(
                        "count_divs", "count the number of <div> elements", 
                        countMatches "<div",
                        taskHeartbeatTimeout       = 60, 
                        taskScheduleToStartTimeout = 10,
                        taskStartToCloseTimeout    = 10, 
                        taskScheduleToCloseTimeout = 20)

let countScripts = Activity<string, int>(
                        "count_scripts", "count the number of <script> elements", 
                        countMatches "<script",
                        taskHeartbeatTimeout       = 60, 
                        taskScheduleToStartTimeout = 10,
                        taskStartToCloseTimeout    = 10, 
                        taskScheduleToCloseTimeout = 20)

let countSpans = Activity<string, int>(
                        "count_spans", "count the number of <span> elements", 
                        countMatches "<span",
                        taskHeartbeatTimeout       = 60, 
                        taskScheduleToStartTimeout = 10,
                        taskStartToCloseTimeout    = 10, 
                        taskScheduleToCloseTimeout = 20)

let countActivities = [| countDivs      :> ISchedulable
                         countScripts   :> ISchedulable
                         countSpans     :> ISchedulable |]

let countReducer (results : Dictionary<int, string>) =
    sprintf "Divs : %d\nScripts : %d\nSpans : %d\n" (int results.[0]) (int results.[1]) (int results.[2])

let countElementsWorkflow = 
    Workflow(domain = "theburningmonk.com", name = "count_html_elements", 
             description = "this workflow counts HTML elements", 
             version = "1")
    ++> echoActivity
    ++> (countActivities, countReducer)
    ++> echoActivity

let awsKey      = "PUT-YOUR-AWS-KEY-HERE"
let awsSecret   = "PUT-YOUR-AWS-SECRET-HERE"
let client = new AmazonSimpleWorkflowClient(awsKey, awsSecret)

countElementsWorkflow.Start(client)