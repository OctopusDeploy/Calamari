namespace Calamari.Integration.Processes.Semaphores
{
    public interface IProcessFinder
    {
        bool ProcessIsRunning(int processId, string processName);
    }
}