using System;
using System.Collections.Generic;

namespace PortTraining.Core.WorkOrders
{
    public sealed class WorkOrderStateMachine
    {
        private static readonly IReadOnlyDictionary<WorkOrderState, WorkOrderState[]> AllowedTransitions =
            new Dictionary<WorkOrderState, WorkOrderState[]>
            {
                [WorkOrderState.PendingAcceptance] = new[] { WorkOrderState.Accepted, WorkOrderState.Terminated },
                [WorkOrderState.Accepted] = new[] { WorkOrderState.Preflight, WorkOrderState.Terminated },
                [WorkOrderState.Preflight] = new[] { WorkOrderState.Ready, WorkOrderState.Terminated },
                [WorkOrderState.Ready] = new[] { WorkOrderState.Running, WorkOrderState.Terminated },
                [WorkOrderState.Running] = new[] { WorkOrderState.Paused, WorkOrderState.Completed, WorkOrderState.Terminated },
                [WorkOrderState.Paused] = new[] { WorkOrderState.Running, WorkOrderState.Terminated },
                [WorkOrderState.Completed] = new[] { WorkOrderState.Scored },
                [WorkOrderState.Scored] = new[] { WorkOrderState.Archived },
                [WorkOrderState.Archived] = Array.Empty<WorkOrderState>(),
                [WorkOrderState.Terminated] = new[] { WorkOrderState.Scored }
            };

        public WorkOrderState State { get; private set; } = WorkOrderState.PendingAcceptance;

        public bool CanTransitionTo(WorkOrderState next)
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

        public void TransitionTo(WorkOrderState next)
        {
            if (!CanTransitionTo(next))
            {
                throw new InvalidOperationException($"Work order transition {State} -> {next} is not allowed.");
            }

            State = next;
        }
    }
}

