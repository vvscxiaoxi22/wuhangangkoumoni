namespace PortTraining.Control.Input
{
    public interface IControlInputProvider
    {
        string SourceId { get; }
        bool IsAvailable { get; }
        OperatorInputSnapshot Read();
    }
}

