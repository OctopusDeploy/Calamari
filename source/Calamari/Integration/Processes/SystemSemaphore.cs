using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace Calamari.Integration.Processes
{
    public class SystemSemaphore : ISemaphore
    {
        public IDisposable Acquire(string name, string waitMessage)
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
            if (!mutex.WaitOne(3000))
            {
                Log.Verbose(waitMessage);
                mutex.WaitOne();
            }

            return new MutexReleaser(mutex);
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

        class MutexReleaser : IDisposable
        {
            readonly Mutex mutex;

            public MutexReleaser(Mutex mutex)
            {
                this.mutex = mutex;
            }

            public void Dispose()
            {
                mutex.ReleaseMutex();
                mutex.Dispose();
            }
        }
    }
}