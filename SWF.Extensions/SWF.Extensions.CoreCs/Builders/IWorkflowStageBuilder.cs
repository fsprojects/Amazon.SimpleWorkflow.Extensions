using System;
using System.Collections.Generic;

using Amazon.SimpleWorkflow.Extensions;

namespace SWF.Extensions.CoreCs.Builders
{
    /// <summary>
    /// Represents a builder for gradually building up the stages of a workflow
    /// </summary>
    public interface IWorkflowStageBuilder
    {
        /// <summary>
        /// Attaches an activity or child workflow as the next stage of a workflow
        /// </summary>
        IWorkflowStageBuilder Attach(ISchedulable schedulable);

        /// <summary>
        /// Attaches an array of schedulables (activity or child workflow) to be executed in parallel, and 
        /// a func delegate to 
        /// </summary>
        IWorkflowStageBuilder Attach(ISchedulable[] schedulables, Func<Dictionary<int, string>, string> reducer);

        /// <summary>
        /// Closes off the workflow
        /// </summary>
        IWorkflow Complete();
    }
}