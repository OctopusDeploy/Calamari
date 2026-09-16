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

namespace Calamari.Common.Features.Processes.NamedLocks
{
    public class MutexBasedNamedLockManager : INamedLockManager
    {
        readonly ILog log;
        readonly int initialWaitBeforeShowingLogMessage;
        readonly ResiliencePipeline mutexAcquisitionPipeline;

        public MutexBasedNamedLockManager()
        {
            log = ConsoleLog.Instance;
            initialWaitBeforeShowingLogMessage = (int)TimeSpan.FromSeconds(3).TotalMilliseconds;

            mutexAcquisitionPipeline = new ResiliencePipelineBuilder()
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

            var acquired = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            Exception? acquisitionFailure = null;

            // A Mutex can only be waited on and released by the thread that acquired it
            var owner = new Thread(() =>
                                    {
                                        Mutex? mutex = null;
                                        try
                                        {
                                            // Create/acquire the global mutex with some retry, to (hopefully) avoid two instances of
                                            // Calamari racing to create it (e.g. parallel steps on the same machine)
                                            mutex = mutexAcquisitionPipeline.Execute(() => new Mutex(false, globalName));

                                            // Assign full control for all users, so that a lock taken by (say) a Tentacle running as a service
                                            // is still accessible to Calamari running under a different account
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
                                                // The previous owner died without releasing; the kernel has handed ownership to us
                                                log.Warn($"The lock '{name}' was abandoned by a previous process that exited without releasing it. Continuing, but anything it was protecting may have been left in an inconsistent state.");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            // We never took the mutex, so there is nothing to release - just
                                            // close the handle rather than leaving it to the finaliser
                                            mutex?.Dispose();
                                            acquisitionFailure = ex;
                                            acquired.Set();
                                            return;
                                        }

                                        acquired.Set();

                                        release.Wait();

                                        // An unhandled exception here would terminate the process. Releasing is best effort:
                                        // a failure leaves the mutex abandoned, which the next waiter recovers from.
                                        try
                                        {
                                            try
                                            {
                                                mutex.ReleaseMutex();
                                            }
                                            finally
                                            {
                                                mutex.Dispose();
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            log.Verbose($"Failed to release the mutex '{globalName}': {ex.PrettyPrint()}");
                                        }
                                    })
                         {
                             IsBackground = true,
                             Name = $"Mutex owner for '{name}'"
                         };
            owner.Start();
            acquired.Wait();

            var releaser = new Releaser(owner, acquired, release);

            if (acquisitionFailure != null)
            {
                releaser.Dispose();
                ExceptionDispatchInfo.Capture(acquisitionFailure).Throw();
            }

            return releaser;
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
            readonly Thread owner;
            readonly ManualResetEventSlim acquired;
            readonly ManualResetEventSlim release;
            int released;

            public Releaser(Thread owner, ManualResetEventSlim acquired, ManualResetEventSlim release)
            {
                this.owner = owner;
                this.acquired = acquired;
                this.release = release;
            }

            public void Dispose()
            {
                // The events are gone after the first time through, so guard against a caller disposing twice
                if (Interlocked.Exchange(ref released, 1) != 0)
                    return;

                release.Set();
                owner.Join();
                acquired.Dispose();
                release.Dispose();
            }
        }
    }
}
