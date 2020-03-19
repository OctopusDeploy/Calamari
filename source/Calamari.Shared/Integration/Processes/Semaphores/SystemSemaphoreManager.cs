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
            this.log = ConsoleLog.Instance;
            this.initialWaitBeforeShowingLogMessage = (int)TimeSpan.FromSeconds(3).TotalMilliseconds;
        }

        public SystemSemaphoreManager(ILog log, TimeSpan initialWaitBeforeShowingLogMessage)
        {
            this.log = log;
            this.initialWaitBeforeShowingLogMessage = (int)initialWaitBeforeShowingLogMessage.TotalMilliseconds;
        }

        public IDisposable Acquire(string name, string waitMessage)
        {
            return CalamariEnvironment.IsRunningOnWindows
                ? AcquireSemaphore(name, waitMessage)
                : AcquireMutex(name, waitMessage);
        }

        IDisposable AcquireSemaphore(string name, string waitMessage)
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


        IDisposable AcquireMutex(string name, string waitMessage)
        {
            Mutex mutex;
            var globalName = $"Global\\{name}";
            try
            {
                mutex = CreateGlobalMutexAccessibleToEveryone(globalName);
            }
            catch (Exception)
            {
                mutex = new Mutex(false, globalName);
            }

            try
            {
                if (!mutex.WaitOne(initialWaitBeforeShowingLogMessage))
                {
                    log.Verbose(waitMessage);
                    mutex.WaitOne();
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
                mutex.ReleaseMutex();
                mutex.Dispose();
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


        static Mutex CreateGlobalMutexAccessibleToEveryone(string name)
        {
            var semaphoreSecurity = new MutexSecurity();
            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var rule = new MutexAccessRule(everyone, MutexRights.FullControl, AccessControlType.Allow);

            semaphoreSecurity.AddAccessRule(rule);

            bool createdNew;
            
            var mutex = new Mutex(false, name, out createdNew);
            mutex.SetAccessControl(semaphoreSecurity);
            return mutex;
        }

        class Releaser : IDisposable
        {
            private readonly Action dispose;

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