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

        // Held so the abandoned holder's handle is never closed or finalised, keeping the kernel object
        // alive exactly as a second interested process would. Released in teardown.
        IDisposable abandonedHolder;

        [TearDown]
        public void ReleaseAbandonedHolder()
        {
            try
            {
                // Unblocks any waiter still stuck on the abandoned holder, so a failing test does not
                // leave a permanently blocked thread pool thread behind.
                abandonedHolder?.Dispose();
            }
            catch (Exception)
            {
                // A Mutex can only be released by its owning thread, which has deliberately exited.
            }
            finally
            {
                abandonedHolder = null;
            }
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
                        "Acquire() never returned after the holder was abandoned. A named Semaphore cannot signal "
                        + "abandonment, so the count stays at zero and the unbounded WaitOne() blocks forever. "
                        + "Using a Mutex on Windows would hand ownership to this waiter instead.");
        }
    }
}
