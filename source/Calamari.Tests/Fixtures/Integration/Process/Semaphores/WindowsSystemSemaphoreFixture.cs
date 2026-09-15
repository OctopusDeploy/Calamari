using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    [SupportedOSPlatform("windows")]
    public class WindowsSystemSemaphoreFixture : SemaphoreFixtureBase
    {
        // Acquire() must recover when the process holding the lock goes away without running the Releaser
        // (killed mid-ApplyRetention, Tentacle restart, OOM). This is why the Windows path uses a named Mutex:
        // an abandoned Mutex is signalled by the kernel and handed to the next waiter, whereas a Semaphore has
        // no notion of ownership and would leave its count at zero, hanging every later waiter forever.

        // Must comfortably exceed the 3s initial wait inside SystemSemaphoreManager.
        static readonly TimeSpan RecoveryAllowance = TimeSpan.FromSeconds(15);

        // Held so the abandoned mutex's handle is never closed or finalised during the test, keeping the
        // kernel object alive exactly as a second interested process would. It cannot be released from here:
        // a Mutex can only be released by its owning thread, which has deliberately exited. Dropping the
        // reference in teardown lets the finaliser close the handle.
        Mutex abandonedMutex;

        [TearDown]
        public void DropAbandonedMutex()
        {
            abandonedMutex = null;
        }

        [Test]
        public void AcquireRecoversWhenTheHolderIsAbandoned()
        {
            var name = $"Octopus.Calamari.AbandonedHolder.{Guid.NewGuid():N}";
            var globalName = $@"Global\{name}";
            var sut = new SystemSemaphoreManager();

            // Simulate the lock being abandoned by some other process entirely (a Calamari process killed
            // mid-ApplyRetention): take the raw named Mutex directly on a thread that then exits without ever
            // releasing it. Acquire() now owns and releases its side of the lock on its own dedicated thread
            // (see SystemSemaphoreManager), so it is no longer possible to simulate abandonment by having a
            // caller's thread die - that thread was never the OS-level owner in the first place.
            var holder = new Thread(() =>
                                     {
                                         abandonedMutex = new Mutex(false, globalName);
                                         abandonedMutex.WaitOne();
                                     });
            holder.Start();
            holder.Join();

            var acquire = Task.Run(() =>
                                   {
                                       using (sut.Acquire(name, "Another process is using the package journal"))
                                       {
                                       }
                                   });

            Assert.That(acquire.Wait(RecoveryAllowance),
                        Is.True,
                        "Acquire() never returned after the holder was abandoned. The lock must be a named Mutex so "
                        + "that the kernel signals abandonment and hands ownership to this waiter; a Semaphore has no "
                        + "owner, so its count stays at zero and the unbounded WaitOne() blocks forever.");
        }

        [Test]
        public async Task ReleasingFromADifferentThreadThanAcquiredSucceeds()
        {
            var name = $"Octopus.Calamari.CrossThreadRelease.{Guid.NewGuid():N}";
            var sut = new SystemSemaphoreManager();

            // Acquire on whatever thread this async method happens to be running on right now.
            var acquiringThread = Thread.CurrentThread.ManagedThreadId;
            var releaser = sut.Acquire(name, "Another process is using the package journal");

            // Hop onto other thread-pool threads via delay/yield, exactly what happens to any async method
            // that awaits something after taking the lock. ConfigureAwait(false) lets the continuation land on
            // whichever pool thread is free rather than being marshalled back, so this reliably changes threads.
            int releasingThread;
            do
            {
                await Task.Delay(1).ConfigureAwait(false);
                await Task.Yield();
                releasingThread = Thread.CurrentThread.ManagedThreadId;
            } while (releasingThread == acquiringThread);

            // A raw Mutex can only be released by the thread that acquired it, so SystemSemaphoreManager
            // acquires and releases on its own dedicated thread internally. Disposing here - from a thread
            // other than the one that called Acquire() - must not throw.
            Assert.DoesNotThrow(() => releaser.Dispose());

            // And the lock must actually have been released: a second Acquire() should succeed promptly rather
            // than hanging behind a lock nobody is going to release.
            var reacquire = Task.Run(() =>
                                     {
                                         using (sut.Acquire(name, "Another process is using the package journal"))
                                         {
                                         }
                                     });

            Assert.That(reacquire.Wait(TimeSpan.FromSeconds(5)),
                        Is.True,
                        "Dispose() returned without actually releasing the mutex.");
        }
    }
}
