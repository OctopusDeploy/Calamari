using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using Calamari.FullFrameworkTools.Contracts;

namespace Calamari.FullFrameworkTools.WindowsCertStore.Semaphores
{
    public class SystemSemaphoreManager : ISemaphoreFactory
    {
        readonly ILog log;
        readonly int initialWaitBeforeShowingLogMessage;

        public SystemSemaphoreManager(ILog log): this(log, TimeSpan.FromSeconds(3))
        {
            
        }
        public SystemSemaphoreManager(ILog log, TimeSpan initialWaitBeforeShowingLogMessage)
        {
            this.log = log;
            this.initialWaitBeforeShowingLogMessage = (int)initialWaitBeforeShowingLogMessage.TotalMilliseconds;
        }

        public IDisposable Acquire(string name, string waitMessage)
        {
            return AcquireSemaphore(name, waitMessage);
        }

        IDisposable AcquireSemaphore(string name, string waitMessage)
        {
            Semaphore semaphore;
            var globalName = $"Global\\{name}";
            try
            {
                semaphore = CreateGlobalSemaphoreAccessibleToEveryone(globalName);
            }
            catch (Exception ex)
            {
                log.Verbose($"Acquiring semaphore failed: {ex.PrettyPrint()}");
                log.Verbose("Retrying without setting access controls...");
                semaphore = new Semaphore(1, 1, globalName);
            }

            try
            {
                if (!semaphore.WaitOne(initialWaitBeforeShowingLogMessage))
                {
                    log.Verbose(waitMessage);
                    semaphore.WaitOne();
                }
            }
            catch (AbandonedMutexException)
            {
                // We are now the owners of the mutex
                // If a thread terminates while owning a mutex, the mutex is said to be abandoned.
                // The state of the mutex is set to signaled and the next waiting thread gets ownership.
            }

            return new Releaser(() =>
            {
                semaphore.Release();
                semaphore.Dispose();
            });
        }

        static Semaphore CreateGlobalSemaphoreAccessibleToEveryone(string name)
        {
            var semaphoreSecurity = new SemaphoreSecurity();
            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var rule = new SemaphoreAccessRule(everyone, SemaphoreRights.FullControl, AccessControlType.Allow);

            semaphoreSecurity.AddAccessRule(rule);

            bool createdNew;

            var semaphore = new Semaphore(1, 1, name, out createdNew);
            semaphore.SetAccessControl(semaphoreSecurity);
            return semaphore;
        }

        class Releaser : IDisposable
        {
            readonly Action dispose;

            public Releaser(Action dispose)
            {
                this.dispose = dispose;
            }

            public void Dispose()
            {
                dispose();
            }
        }
    }
}