using System;
using System.Collections.Generic;
using Amazon.SimpleWorkflow.Extensions;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;

namespace SWF.Extensions.CoreCs.Builders
{
    using ChildPolicy = Amazon.SimpleWorkflow.Extensions.Model.ChildPolicy;

    internal sealed class WorkflowStageBuilderImpl : IWorkflowStageBuilder
    {
        private readonly IWorkflowBuilder workflowBuilder;
        private readonly List<Stage> stages = new List<Stage>();

        public WorkflowStageBuilderImpl(IWorkflowBuilder workflowBuilder)
        {
            this.workflowBuilder = workflowBuilder;
        }

        public IWorkflowStageBuilder Attach(ISchedulable schedulable)
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

        public IWorkflowStageBuilder Attach(ISchedulable[] schedulables, Func<Dictionary<int, string>, string> reducer)
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
                    workflowBuilder.Domain,
                    workflowBuilder.Name,
                    workflowBuilder.Description,
                    workflowBuilder.Version,
                    workflowBuilder.TaskList.AsOption(string.IsNullOrWhiteSpace),
                    ListModule.OfSeq(stages).AsOption(),
                    workflowBuilder.TaskStartToCloseTimeout.AsOption(),
                    workflowBuilder.ExecutionStartToCloseTimeout.AsOption(),
                    GetChildPolicy(workflowBuilder.ChildPolicy).AsOption(),
                    workflowBuilder.Identity.AsOption(string.IsNullOrWhiteSpace),
                    workflowBuilder.MaxAttempts.AsOption());
        }

        /// <summary>
        /// Converts the more C# friendly ChildPolicyEnum representation to the F# DU type
        /// </summary>
        private static ChildPolicy GetChildPolicy(ChildPolicyEnum childPolicyEnum)
        {
            switch (childPolicyEnum)
            {
                case ChildPolicyEnum.Terminate:
                    return ChildPolicy.Terminate;
                case ChildPolicyEnum.Abandon:
                    return ChildPolicy.Abandon;
                case ChildPolicyEnum.RequestCancel:
                    return ChildPolicy.RequestCancel;
                default:
                    throw new NotSupportedException(string.Format("ChildPolicyEnum [{0}] is not supported", childPolicyEnum));
            }
        }
    }
}