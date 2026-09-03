using System;

namespace PortTraining.YardCraneSimulator.Bootstrap
{
    public sealed class YardCraneSceneStateMachine
    {
        public YardCraneSceneState Current { get; private set; } = YardCraneSceneState.Booting;

        public event Action<YardCraneSceneState, YardCraneSceneState> Changed;

        public bool TryTransition(YardCraneSceneState next, out string error)
        {
            if (!IsAllowed(Current, next))
            {
                error = $"Invalid yard-crane scene transition: {Current} -> {next}.";
                return false;
            }

            var previous = Current;
            Current = next;
            error = string.Empty;
            Changed?.Invoke(previous, next);
            return true;
        }

        private static bool IsAllowed(YardCraneSceneState current, YardCraneSceneState next)
        {
            if (current == next)
            {
                return true;
            }

            if (next == YardCraneSceneState.Failed || next == YardCraneSceneState.Terminated)
            {
                return current != YardCraneSceneState.ExitAllowed &&
                       current != YardCraneSceneState.Terminated &&
                       current != YardCraneSceneState.Failed;
            }

            return current switch
            {
                YardCraneSceneState.Booting => next == YardCraneSceneState.WaitingForCoordinator,
                YardCraneSceneState.WaitingForCoordinator => next == YardCraneSceneState.Loading,
                YardCraneSceneState.Loading => next == YardCraneSceneState.Ready,
                YardCraneSceneState.Ready => next == YardCraneSceneState.Running,
                YardCraneSceneState.Running => next == YardCraneSceneState.Paused || next == YardCraneSceneState.Finalizing,
                YardCraneSceneState.Paused => next == YardCraneSceneState.Running || next == YardCraneSceneState.Finalizing,
                YardCraneSceneState.Finalizing => next == YardCraneSceneState.ExitAllowed,
                _ => false
            };
        }
    }
}

