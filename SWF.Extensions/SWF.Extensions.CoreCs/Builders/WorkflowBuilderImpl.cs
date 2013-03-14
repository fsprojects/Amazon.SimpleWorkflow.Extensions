using System;
using System.Collections.Generic;

using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;

namespace Amazon.SimpleWorkflow.Extensions.Builders
{
    internal sealed class WorkflowBuilderImpl : IWorkflowBuilder
    {
        private readonly List<Stage> stages = new List<Stage>();

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

        public IWorkflowBuilder Attach(ISchedulable schedulable)
        {
            StageAction stageAction;
            if (schedulable is IActivity)
            {
                stageAction = StageAction.NewScheduleActivity((IActivity)schedulable);
            }
            else if (schedulable is IWorkflow)
            {
                stageAction = StageAction.NewStartChildWorkflow((IWorkflow)schedulable);
            }
            else
            {
                throw new NotSupportedException(string.Format(
                    "Type [{0}] is not a supported schedulable action",
                    schedulable.GetType()));
            }

            var stage = new Stage(stages.Count, stageAction);
            stages.Add(stage);

            return this;
        }

        public IWorkflowBuilder Attach(ISchedulable[] schedulables, Func<Dictionary<int, string>, string> reducer)
        {
            var fsharpReducer = FuncConvert.ToFSharpFunc(new Converter<Dictionary<int, string>, string>(reducer));
            var stageAction = StageAction.NewParallelActions(schedulables, fsharpReducer);
            var stage = new Stage(stages.Count, stageAction);
            stages.Add(stage);

            return this;
        }

        public IWorkflow Complete()
        {
            return new Workflow(
                    Domain,
                    Name,
                    Description,
                    Version,
                    TaskList.AsOption(string.IsNullOrWhiteSpace),
                    ListModule.OfSeq(stages).AsOption(),
                    TaskStartToCloseTimeout.AsOption(),
                    ExecutionStartToCloseTimeout.AsOption(),
                    GetChildPolicy(ChildPolicy).AsOption(),
                    Identity.AsOption(string.IsNullOrWhiteSpace),
                    MaxAttempts.AsOption());
        }

        /// <summary>
        /// Converts the more C# friendly ChildPolicyEnum representation to the F# DU type
        /// </summary>
        private static Model.ChildPolicy GetChildPolicy(ChildPolicyEnum childPolicyEnum)
        {
            switch (childPolicyEnum)
            {
                case ChildPolicyEnum.Terminate:
                    return Model.ChildPolicy.Terminate;
                case ChildPolicyEnum.Abandon:
                    return Model.ChildPolicy.Abandon;
                case ChildPolicyEnum.RequestCancel:
                    return Model.ChildPolicy.RequestCancel;
                default:
                    throw new NotSupportedException(string.Format("ChildPolicyEnum [{0}] is not supported", childPolicyEnum));
            }
        }
    }
}