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

            Console.ReadKey();
        }

        private static void Start()
        {
            var client = new AmazonSimpleWorkflowClient();
        }
    }
}
