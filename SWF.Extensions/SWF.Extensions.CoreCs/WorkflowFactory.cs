using System;

using Amazon.SimpleWorkflow.Extensions.CoreCS.Builders;

namespace Amazon.SimpleWorkflow.Extensions.CoreCS
{
    public static class WorkflowFactory
    {
        public static IWorkflowBuilder CreateWorkflow(string domain, string name, string version)
        {
            return new WorkflowBuilderImpl(domain, name, version);
        }

        public static IActivityBuilder<TInput, TOutput> CreateActivity<TInput, TOutput>(
            string name,
            Func<TInput, TOutput> processor,
            int taskHeartbeatTimeout,
            int taskScheduleToStartTimeout,
            int taskStartToCloseTimeout,
            int taskScheduleToCloseTimeout)
        {
            return new ActivityBuilderImpl<TInput, TOutput>(
                        name, 
                        processor, 
                        taskHeartbeatTimeout, 
                        taskScheduleToStartTimeout,
                        taskStartToCloseTimeout,
                        taskScheduleToCloseTimeout);
        }
    }
}
