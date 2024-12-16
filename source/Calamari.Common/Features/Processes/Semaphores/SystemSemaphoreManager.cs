using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Polly;
using Polly.Retry;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public class SystemSemaphoreManager : ISemaphoreFactory
    {
        readonly ILog log;
        readonly int initialWaitBeforeShowingLogMessage;
        readonly ResiliencePipeline semaphoreAcquisitionPipeline;

        public SystemSemaphoreManager()
        {
            log = ConsoleLog.Instance;
            initialWaitBeforeShowingLogMessage = (int)TimeSpan.FromSeconds(3).TotalMilliseconds;

            semaphoreAcquisitionPipeline = new ResiliencePipelineBuilder()
                                           .AddRetry(new RetryStrategyOptions()
                                           {
                                               ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                                               MaxRetryAttempts = 5, //means we'll wait for a max of around 250ms
                                               BackoffType = DelayBackoffType.Linear,
                                               UseJitter = true,
                                               Delay = TimeSpan.FromMilliseconds(50),
                                               OnRetry = args =>
                                                         {
                                                             log.Verbose($"Waiting {args.RetryDelay.TotalMilliseconds}ms before attempting to acquire the Semaphore again");
                                                             return default;
                                                         }
                                           })
                                           .Build();
        }

        public IDisposable Acquire(string name, string waitMessage)
        {
            return CalamariEnvironment.IsRunningOnWindows
                ? AcquireSemaphore(name, waitMessage)
                : AcquireMutex(name, waitMessage);
        }

        IDisposable AcquireSemaphore(string name, string waitMessage)
        {
            var globalName = $"Global\\{name}";

            var semaphore = CreateGlobalSemaphoreAccessibleToEveryone(globalName);

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
            var globalName = $"Global\\{name}";
            var mutex = new Mutex(false, globalName);

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

        Semaphore CreateGlobalSemaphoreAccessibleToEveryone(string name)
        {
            var semaphoreSecurity = new SemaphoreSecurity();
            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var rule = new SemaphoreAccessRule(everyone, SemaphoreRights.FullControl, AccessControlType.Allow);

            semaphoreSecurity.AddAccessRule(rule);

            //we try and create/acquire a global semaphore with some retry
            var semaphore = semaphoreAcquisitionPipeline.Execute(() => new Semaphore(1,1, name));

            try
            {
                semaphore.SetAccessControl(semaphoreSecurity);
            }
            catch (Exception e)
            {
                log.Verbose($"Failed to set access controls on semaphore '{name}': {e.PrettyPrint()}");
                //at this point we have a valid semaphore, which is what we'd end up doing anyway
            }

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