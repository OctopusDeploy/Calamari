using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace Calamari.Integration.Processes.Semaphores
{
    public class SystemSemaphoreManager : ISemaphoreFactory
    {
        private readonly ILog log;
        private readonly int initialWaitBeforeShowingLogMessage;

        public SystemSemaphoreManager()
        {
            this.log = new LogWrapper();
            this.initialWaitBeforeShowingLogMessage = (int)TimeSpan.FromSeconds(3).TotalMilliseconds;
        }

        public SystemSemaphoreManager(ILog log, TimeSpan initialWaitBeforeShowingLogMessage)
        {
            this.log = log;
            this.initialWaitBeforeShowingLogMessage = (int)initialWaitBeforeShowingLogMessage.TotalMilliseconds;
        }

        public IDisposable Acquire(string name, string waitMessage)
        {
            Semaphore semaphore;
            var globalName = $"Global\\{name}";
            try
            {
                semaphore = CreateGlobalSemaphoreAccessibleToEveryone(globalName);
            }
            catch (Exception)
            {
                semaphore = new Semaphore(1, 1, globalName);
            }
            if (!semaphore.WaitOne(initialWaitBeforeShowingLogMessage))
            {
                log.Verbose(waitMessage);
                semaphore.WaitOne();
            }

            return new SystemSemaphoreReleaser(semaphore);
        }

        static Semaphore CreateGlobalSemaphoreAccessibleToEveryone(string name)
        {
            var semaphoreSecurity = new SemaphoreSecurity();
            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var rule = new SemaphoreAccessRule(everyone, SemaphoreRights.FullControl, AccessControlType.Allow);

            semaphoreSecurity.AddAccessRule(rule);
            bool createdNew;

            var semaphore = new Semaphore(1, 1, name, out createdNew, semaphoreSecurity);
            return semaphore;
        }

        class SystemSemaphoreReleaser : IDisposable
        {
            readonly Semaphore semaphore;

            public SystemSemaphoreReleaser(Semaphore semaphore)
            {
                this.semaphore = semaphore;
            }

            public void Dispose()
            {
                semaphore.Release();
                semaphore.Dispose();
            }
        }
    }
}