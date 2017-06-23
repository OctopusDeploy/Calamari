namespace Calamari.Integration.Processes.Semaphores
{
    public static class SemaphoreFactory
    {
        public static ISemaphoreFactory Get()
        {
            // TODO: Should we always use the system semaphore?
            if (CalamariEnvironment.IsRunningOnMono)
                return new FileBasedSempahoreManager();
            return new SystemSemaphoreManager();
        }
    }
}