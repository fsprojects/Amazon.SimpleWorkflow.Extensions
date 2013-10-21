using System;

namespace Amazon.SimpleWorkflow.Extensions.Builders
{
    /// <summary>
    /// Represents a builder for gradually building up the skeletons of an activity
    /// </summary>
    public interface IActivityBuilder<TInput, TOutput>
    {
        string Name { get; }

        string Version { get; }

        string Description { get; }

        string TaskList { get; }

        Func<TInput, TOutput> Processor { get; }

        int TaskHeartbeatTimeout { get; }

        int TaskScheduleToStartTimeout { get; }

        int TaskStartToCloseTimeout { get; }

        int TaskScheduleToCloseTimeout { get; }

        int? MaxAttempts { get; }

        /// <summary>
        /// Optionally sets the description for the activity
        /// </summary>
        IActivityBuilder<TInput, TOutput> WithDescription(string description);

        /// <summary>
        /// Optionally sets the name of the tasklist to pull tasks from
        /// </summary>
        IActivityBuilder<TInput, TOutput> WithTaskList(string taskList);

        /// <summary>
        /// Optionally sets the max number of attempts that can be made to execute the activity
        /// </summary>
        IActivityBuilder<TInput, TOutput> WithMaxAttempts(int maxAttempts);

        /// <summary>
        /// Finish editing the activity
        /// </summary>
        IActivity Complete();

        IActivityBuilder<TInput, TOutput> WithVersion(string version);
    }
}