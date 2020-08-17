using System;
using Calamari.Common.Plumbing;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public static class SemaphoreFactory
    {
        public static ISemaphoreFactory Get()
        {
            if (CalamariEnvironment.IsRunningOnMono)
                return new FileBasedSempahoreManager();
            return new SystemSemaphoreManager();
        }
    }
}