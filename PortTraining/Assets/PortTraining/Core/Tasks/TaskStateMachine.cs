using System;
using System.Collections.Generic;

namespace PortTraining.Core.Tasks
{
    public sealed class TaskStateMachine
    {
        private static readonly IReadOnlyDictionary<TaskExecutionState, TaskExecutionState[]> AllowedTransitions =
            new Dictionary<TaskExecutionState, TaskExecutionState[]>
            {
                [TaskExecutionState.PendingAcceptance] = new[] { TaskExecutionState.Accepted, TaskExecutionState.Terminated },
                [TaskExecutionState.Accepted] = new[] { TaskExecutionState.Preflight, TaskExecutionState.Terminated },
                [TaskExecutionState.Preflight] = new[] { TaskExecutionState.Ready, TaskExecutionState.Terminated },
                [TaskExecutionState.Ready] = new[] { TaskExecutionState.Running, TaskExecutionState.Terminated },
                [TaskExecutionState.Running] = new[] { TaskExecutionState.Paused, TaskExecutionState.Completed, TaskExecutionState.Terminated },
                [TaskExecutionState.Paused] = new[] { TaskExecutionState.Running, TaskExecutionState.Terminated },
                [TaskExecutionState.Completed] = new[] { TaskExecutionState.Scored },
                [TaskExecutionState.Scored] = new[] { TaskExecutionState.Archived },
                [TaskExecutionState.Archived] = Array.Empty<TaskExecutionState>(),
                [TaskExecutionState.Terminated] = new[] { TaskExecutionState.Scored }
            };

        public TaskExecutionState State { get; private set; } = TaskExecutionState.PendingAcceptance;

        public bool CanTransitionTo(TaskExecutionState next)
        {
            foreach (var candidate in AllowedTransitions[State])
            {
                if (candidate == next)
                {
                    return true;
                }
            }

            return false;
        }

        public void TransitionTo(TaskExecutionState next)
        {
            if (!CanTransitionTo(next))
            {
                throw new InvalidOperationException($"Task transition {State} -> {next} is not allowed.");
            }

            State = next;
        }
    }
}

