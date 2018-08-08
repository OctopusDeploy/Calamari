using Calamari.Shared;

namespace Calamari.Integration.Processes.Semaphores
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