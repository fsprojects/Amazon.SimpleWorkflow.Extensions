using System;
using System.Collections.Generic;

using Amazon.SimpleWorkflow.Extensions.CoreCs;

namespace Amazon.SimpleWorkflow.Extensions.CoreCS.Builders
{
    /// <summary>
    /// Represents a builder for gradually building up the skeletons of a workflow
    /// </summary>
    public interface IWorkflowBuilder
    {
        string Domain { get; }

        string Name { get; }

        string Version { get; }

        string TaskList { get; }

        string Description { get; }

        int? TaskStartToCloseTimeout { get; }

        int? ExecutionStartToCloseTimeout { get; }

        ChildPolicyEnum ChildPolicy { get; }

        string Identity { get; }

        int? MaxAttempts { get; }

        /// <summary>
        /// Optionally sets the description for the workflow
        /// </summary>
        IWorkflowBuilder WithDescription(string description);

        /// <summary>
        /// Optionally sets the name of the tasklist to pull tasks from
        /// </summary>
        IWorkflowBuilder WithTaskList(string taskList);

        /// <summary>
        /// Optionally sets the timeout in seconds for how long a decision task can take to execute
        /// </summary>
        IWorkflowBuilder WithTaskStartToCloseTimeout(int seconds);

        /// <summary>
        /// Optionally sets the timeout in seconds for how long the workflow can take to execute as a whole
        /// </summary>
        IWorkflowBuilder WithExecutionStartToCloseTimeout(int seconds);

        /// <summary>
        /// Optionally sets the child policy
        /// </summary>
        IWorkflowBuilder WithChildPolicy(ChildPolicyEnum childPolicy);

        /// <summary>
        /// Optionally sets the identity of the current worker
        /// </summary>
        IWorkflowBuilder WithIdentity(string identity);

        /// <summary>
        /// Optionally sets the max number of attempts that can be made to execute the workflow
        /// </summary>
        IWorkflowBuilder WithMaxAttempts(int maxAttempts);

        /// <summary>
        /// Attaches an activity or child workflow as the next stage of a workflow
        /// </summary>
        IWorkflowBuilder Attach(ISchedulable schedulable);

        /// <summary>
        /// Attaches an array of schedulables (activity or child workflow) to be executed in parallel, and 
        /// a func delegate to 
        /// </summary>
        IWorkflowBuilder Attach(ISchedulable[] schedulables, Func<Dictionary<int, string>, string> reducer);

        /// <summary>
        /// Closes off the workflow
        /// </summary>
        IWorkflow Complete();
    }
}