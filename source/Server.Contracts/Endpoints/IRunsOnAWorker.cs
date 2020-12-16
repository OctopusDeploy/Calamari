namespace Sashimi.Server.Contracts.Endpoints
{
    public interface IRunsOnAWorker
    {
        string? DefaultWorkerPoolId { get; set; }
    }
}