namespace Calamari.Integration.Processes.Semaphores
{
    public static class SemaphoreFactory
    {
        public static ISemaphore Get()
        {
            if (CalamariEnvironment.IsRunningOnMono)
                return new FileBasedSempahore();
            return new SystemSemaphore();
        }
    }
}