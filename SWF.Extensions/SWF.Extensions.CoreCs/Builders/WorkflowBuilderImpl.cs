namespace SWF.Extensions.CoreCs.Builders
{
    internal sealed class WorkflowBuilderImpl : IWorkflowBuilder
    {
        public WorkflowBuilderImpl(string domain, string name, string version)
        {
            Domain = domain;
            Name = name;
            Version = version;
        }

        #region Properties

        public string Domain { get; private set; }

        public string Name { get; private set; }

        public string Version { get; private set; }

        public string Description { get; private set; }

        public string TaskList { get; private set; }

        public int? TaskStartToCloseTimeout { get; private set; }

        public int? ExecutionStartToCloseTimeout { get; private set; }

        public ChildPolicyEnum ChildPolicy { get; private set; }

        public string Identity { get; private set; }

        public int? MaxAttempts { get; private set; }

        #endregion

        public IWorkflowBuilder WithDescription(string description)
        {
            Description = description;
            return this;
        }

        public IWorkflowBuilder WithTaskList(string taskList)
        {
            TaskList = taskList;
            return this;
        }

        public IWorkflowBuilder WithTaskStartToCloseTimeout(int seconds)
        {
            TaskStartToCloseTimeout = seconds;
            return this;
        }

        public IWorkflowBuilder WithExecutionStartToCloseTimeout(int seconds)
        {
            ExecutionStartToCloseTimeout = seconds;
            return this;
        }

        public IWorkflowBuilder WithChildPolicy(ChildPolicyEnum childPolicy)
        {
            ChildPolicy = childPolicy;
            return this;
        }

        public IWorkflowBuilder WithIdentity(string identity)
        {
            Identity = identity;
            return this;
        }

        public IWorkflowBuilder WithMaxAttempts(int maxAttempts)
        {
            MaxAttempts = maxAttempts;
            return this;
        }
    }
}