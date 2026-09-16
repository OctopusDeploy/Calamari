using System;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
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
                                                             log.Verbose($"Waiting {args.RetryDelay.TotalMilliseconds}ms before attempting to acquire the Mutex again");
                                                             return default;
                                                         }
                                           })
                                           .Build();
        }

        public IDisposable Acquire(string name, string waitMessage)
        {
            var globalName = $@"Global\{name}";

            // A Mutex can only be waited on and released by the thread that acquired it, but callers may
            // dispose the returned IDisposable from a different thread than the one that called Acquire -
            // most obviously, any async method that awaits something in between. So the actual WaitOne() and
            // ReleaseMutex() calls happen on a dedicated thread that lives for exactly as long as the lock is
            // held, and Acquire()/Dispose() just hand signals to and from it.
            var acquired = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            Exception acquisitionFailure = null;

            var owner = new Thread(() =>
                                    {
                                        Mutex mutex;
                                        try
                                        {
                                            //we try and create/acquire a global mutex with some retry
                                            //this is done to (hopefully) avoid situations where two instances of Calamari are trying to acquire the same mutex
                                            //this could happen in the case of parallel steps being executed on the same machine
                                            mutex = semaphoreAcquisitionPipeline.Execute(() => new Mutex(false, globalName));

                                            //assign full control for all users, so that a lock taken by (say) a Tentacle running as a service
                                            //is still accessible to Calamari running under a different account
                                            if (OperatingSystem.IsWindows())
                                                SetFullAccessControlForAllUsers(mutex, globalName);

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
                                                // We are now the owners of the mutex.
                                                // If a thread or process terminates while owning a mutex, the mutex is said to be abandoned:
                                                // the kernel signals it and hands ownership to the next waiter. This recovery is the reason a
                                                // Mutex is used here rather than a Semaphore - a Semaphore has no notion of ownership, so a
                                                // holder that died without releasing would leave its count at zero and block every later
                                                // waiter forever.
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            acquisitionFailure = ex;
                                            acquired.Set();
                                            return;
                                        }

                                        acquired.Set();

                                        release.Wait();

                                        mutex.ReleaseMutex();
                                        mutex.Dispose();
                                    })
                         {
                             IsBackground = true,
                             Name = $"Mutex owner for '{name}'"
                         };
            owner.Start();
            acquired.Wait();

            if (acquisitionFailure != null)
                ExceptionDispatchInfo.Capture(acquisitionFailure).Throw();

            return new Releaser(() =>
                                {
                                    release.Set();
                                    owner.Join();
                                });
        }

        [SupportedOSPlatform("windows")]
        void SetFullAccessControlForAllUsers(Mutex mutex, string name)
        {
            var mutexSecurity = new MutexSecurity();
            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var rule = new MutexAccessRule(everyone, MutexRights.FullControl, AccessControlType.Allow);

            mutexSecurity.AddAccessRule(rule);

            try
            {
                mutex.SetAccessControl(mutexSecurity);
            }
            catch (Exception e)
            {
                log.Verbose($"Failed to set access controls on mutex '{name}': {e.PrettyPrint()}");
            }
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
