namespace SWF.Extensions.CoreCs.Builders
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

        IWorkflowBuilder WithDescription(string description);

        IWorkflowBuilder WithTaskList(string taskList);

        IWorkflowBuilder WithTaskStartToCloseTimeout(int seconds);

        IWorkflowBuilder WithExecutionStartToCloseTimeout(int seconds);

        IWorkflowBuilder WithChildPolicy(ChildPolicyEnum childPolicy);

        IWorkflowBuilder WithIdentity(string identity);

        IWorkflowBuilder WithMaxAttempts(int maxAttempts);
    }
}