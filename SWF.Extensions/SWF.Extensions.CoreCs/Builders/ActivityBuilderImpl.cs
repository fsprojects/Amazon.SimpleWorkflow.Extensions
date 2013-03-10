using System;

using Microsoft.FSharp.Core;

namespace Amazon.SimpleWorkflow.Extensions.CoreCS.Builders
{
    internal sealed class ActivityBuilderImpl<TInput, TOutput> : IActivityBuilder<TInput, TOutput>
    {
        public ActivityBuilderImpl(
            string name, 
            Func<TInput, TOutput> processor,
            int taskHeartbeatTimeout,
            int taskScheduleToStartTimeout,
            int taskStartToCloseTimeout,
            int taskScheduleToCloseTimeout)
        {
            Name = name;
            Processor = processor;
            TaskHeartbeatTimeout = taskHeartbeatTimeout;
            TaskScheduleToStartTimeout = taskScheduleToStartTimeout;
            TaskStartToCloseTimeout = taskStartToCloseTimeout;
            TaskScheduleToCloseTimeout = taskScheduleToCloseTimeout;
        }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public string TaskList { get; private set; }

        public Func<TInput, TOutput> Processor { get; private set; }

        public int TaskHeartbeatTimeout { get; private set; }

        public int TaskScheduleToStartTimeout { get; private set; }

        public int TaskStartToCloseTimeout { get; private set; }

        public int TaskScheduleToCloseTimeout { get; private set; }

        public int? MaxAttempts { get; private set; }

        public IActivityBuilder<TInput, TOutput> WithDescription(string description)
        {
            Description = description;
            return this;
        }

        public IActivityBuilder<TInput, TOutput> WithTaskList(string taskList)
        {
            TaskList = taskList;
            return this;
        }

        public IActivityBuilder<TInput, TOutput> WithMaxAttempts(int maxAttempts)
        {
            MaxAttempts = maxAttempts;
            return this;
        }

        public IActivity Complete()
        {
            var processor = FuncConvert.ToFSharpFunc(new Converter<TInput, TOutput>(Processor));

            return new Activity<TInput, TOutput>(
                            Name,
                            Description,
                            processor,
                            TaskHeartbeatTimeout,
                            TaskScheduleToStartTimeout,
                            TaskStartToCloseTimeout,
                            TaskScheduleToCloseTimeout,
                            TaskList.AsOption(string.IsNullOrWhiteSpace),
                            MaxAttempts.AsOption());
        }
    }
}