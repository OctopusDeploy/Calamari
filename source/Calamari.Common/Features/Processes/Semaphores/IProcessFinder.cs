using System;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public interface IProcessFinder
    {
        bool ProcessIsRunning(int processId, string processName);
    }
}