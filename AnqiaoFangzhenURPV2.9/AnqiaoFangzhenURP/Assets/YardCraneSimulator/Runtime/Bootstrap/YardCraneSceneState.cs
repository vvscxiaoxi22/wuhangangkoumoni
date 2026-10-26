namespace PortTraining.YardCraneSimulator.Bootstrap
{
    public enum YardCraneSceneState
    {
        Booting = 0,
        WaitingForCoordinator = 1,
        Loading = 2,
        Ready = 3,
        Running = 4,
        Paused = 5,
        Finalizing = 6,
        ExitAllowed = 7,
        Terminated = 8,
        Failed = 9
    }
}

