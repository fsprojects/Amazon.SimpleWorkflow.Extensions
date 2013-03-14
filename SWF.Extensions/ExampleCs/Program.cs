using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

using Amazon.SimpleWorkflow;
using Amazon.SimpleWorkflow.Extensions;

namespace ExampleCs
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            StartHelloWorldWorkflow();
            StartEchoWorkflow();
            StartCountHtmlElementsWorkflow();

            Console.WriteLine("Workflows started...");
            Thread.Sleep(TimeSpan.FromHours(1));
        }

        private static void StartHelloWorldWorkflow()
        {
            var client = new AmazonSimpleWorkflowClient();

            var activity = WorkflowFactory.CreateActivity<string, string>("hello_world", HelloWorld, 60, 10, 10, 20)
                                          .Complete();
            var workflow = WorkflowFactory.CreateWorkflow("theburningmonk.com", "hello_world_cs", "1")
                                          .Attach(activity)
                                          .Complete();
            
            workflow.Start(client);
        }

        private static void StartEchoWorkflow()
        {
            var client = new AmazonSimpleWorkflowClient();

            var activity = WorkflowFactory.CreateActivity<string, string>("echo", Echo, 60, 10, 10, 20)
                                          .Complete();
            var workflow = WorkflowFactory.CreateWorkflow("theburningmonk.com", "echo_cs", "1")
                                          .Attach(activity)
                                          .Complete();
            
            workflow.Start(client);
        }

        private static void StartCountHtmlElementsWorkflow()
        {
            var client = new AmazonSimpleWorkflowClient();

            var echo = WorkflowFactory.CreateActivity<string, string>("echo", Echo, 60, 10, 10, 20)
                                      .Complete();
            var countDiv = WorkflowFactory.CreateActivity<string, int>("count_divs", address => CountMatches("<div", address), 60, 10, 10, 20)
                                          .WithDescription("count the number of <div> elements")
                                          .Complete();
            var countScripts = WorkflowFactory.CreateActivity<string, int>("count_scripts", address => CountMatches("<script", address), 60, 10, 10, 20)
                                              .WithDescription("count the number of <script> elements")
                                              .Complete();
            var countSpans = WorkflowFactory.CreateActivity<string, int>("count_spans", address => CountMatches("<span", address), 60, 10, 10, 20)
                                            .WithDescription("count the number of <span> elements")
                                            .Complete();

            var workflow = WorkflowFactory.CreateWorkflow("theburningmonk.com", "count_html_elements_cs", "1")
                                          .Attach(echo)
                                          .Attach(new[] { countDiv, countScripts, countSpans }, MatchesReducer)
                                          .Attach(echo)
                                          .Complete();
            
            workflow.Start(client);
        }

        private static string HelloWorld(string _)
        {
            Console.WriteLine("Hello World");
            return "Hello World!";
        }

        private static string Echo(string input)
        {
            Console.WriteLine(input);
            return input;
        }

        private static int CountMatches(string pattern, string address)
        {
            var webClient = new WebClient();
            var html = webClient.DownloadString(address);

            return Enumerable.Range(0, html.Length - pattern.Length)
                             .Select(i => html.Substring(i, pattern.Length))
                             .Count(str => str == pattern);
        }

        private static string MatchesReducer(Dictionary<int, string> results)
        {
            return string.Format("Divs : {0}\nScripts : {1}\nSpans : {2}\n", results[0], results[1], results[2]);
        }
    }
}
