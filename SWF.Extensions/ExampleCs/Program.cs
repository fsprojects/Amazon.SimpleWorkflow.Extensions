using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Amazon;
using Amazon.SimpleWorkflow;
using Amazon.SimpleWorkflow.Extensions;
using Amazon.SimpleWorkflow.Model;

using Microsoft.FSharp.Core;

namespace ExampleCs
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Start();
        }

        private static void Start()
        {
            var client = new AmazonSimpleWorkflowClient();

            var res = client.PollForDecisionTask(new PollForDecisionTaskRequest
                {
                    Domain = "iwi",
                    TaskList = new TaskList { Name = "transformTaskList" }
                });

            //var task = res.PollForDecisionTaskResult.DecisionTask;
            //Console.WriteLine("Received task {0}", task.TaskToken);

            DecisionWorker.Start(client, "iwi", "transformTaskList", CompleteWorkflow, PrintError, 1);

            Console.ReadKey();
        }

        private static Decisions[] CompleteWorkflow(DecisionTask task)
        {
            return new[] { Decisions.NewCompleteWorkflowExecution(new FSharpOption<string>("All done")) };
        }

        private static void PrintError(Exception exn)
        {
            Console.WriteLine(exn);
        }
    }
}
