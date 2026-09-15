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

        // Held so the abandoned holder's handle is never closed or finalised during the test, keeping the
        // kernel object alive exactly as a second interested process would. It cannot be released from here:
        // a Mutex can only be released by its owning thread, which has deliberately exited. Dropping the
        // reference in teardown lets the finaliser close the handle.
        IDisposable abandonedHolder;

        [TearDown]
        public void DropAbandonedHolder()
        {
            abandonedHolder = null;
        }

        [Test]
        public void AcquireRecoversWhenTheHolderIsAbandoned()
        {
            var name = $"Octopus.Calamari.AbandonedHolder.{Guid.NewGuid():N}";
            var sut = new SystemSemaphoreManager();

            // Take the semaphore on a thread that then exits without ever disposing the Releaser. This is
            // what a Calamari process killed mid-ApplyRetention leaves behind: a holder that will never
            // release. Going through Acquire() keeps this independent of which primitive and which name
            // the implementation happens to use.
            var holder = new Thread(() => abandonedHolder = sut.Acquire(name, "Another process is using the package journal"));
            holder.Start();
            holder.Join();

            // Acquire and release on the same thread: a Mutex can only be released by its owning thread,
            // so handing the Releaser back to the test thread would throw regardless of the hang.
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
        public async Task ReleasingFromADifferentThreadThanAcquiredThrows()
        {
            var name = $"Octopus.Calamari.CrossThreadRelease.{Guid.NewGuid():N}";
            var sut = new SystemSemaphoreManager();

            // Acquire on whatever thread this async method happens to be running on right now - that thread
            // becomes the Mutex's owner as far as the kernel is concerned.
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

            // Disposing here releases the Mutex from a thread other than the one that acquired it. A Mutex is
            // owned by a specific thread (not the process), so the kernel rejects the release outright - this is
            // exactly why ISemaphoreFactory.Acquire documents that the returned IDisposable must be disposed on
            // the acquiring thread, and why async code cannot safely hold the lock across an await.
            var ex = Assert.Throws<ApplicationException>(() => releaser.Dispose());
            Assert.That(ex.Message, Does.Contain("unsynchronized"));
        }
    }
}
