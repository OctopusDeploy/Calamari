using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace Calamari.Integration.Processes.Semaphores
{
    public class SystemSemaphore : ISemaphore
    {
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
            if (!semaphore.WaitOne(3000))
            {
                Log.Verbose(waitMessage);
                semaphore.WaitOne();
            }

            return new SemaphoreReleaser(semaphore);
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

        class SemaphoreReleaser : IDisposable
        {
            readonly Semaphore semaphore;

            public SemaphoreReleaser(Semaphore semaphore)
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