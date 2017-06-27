namespace Calamari.Integration.Processes.Semaphores
{
    public static class SemaphoreFactory
    {
        public static ISemaphoreFactory Get()
        {
            if (CalamariEnvironment.IsRunningOnMono || CalamariEnvironment.IsRunningOnMac || CalamariEnvironment.IsRunningOnNix)
                return new FileBasedSempahoreManager();
            return new SystemSemaphoreManager();
        }
    }
}