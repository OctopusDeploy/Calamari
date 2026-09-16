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
        // (killed mid-ApplyRetention, Tentacle restart, OOM).
        // Practically, this is why we use a mutex - a Semaphore won't cut it on Windows. 

        // Must comfortably exceed the 3s initial wait inside SystemSemaphoreManager.
        static readonly TimeSpan RecoveryAllowance = TimeSpan.FromSeconds(15);

        // Held so the abandoned mutex's handle is never closed or finalised during the test
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

            // Simulate the lock being abandoned by some other process entirely
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

            Assert.That(acquire.Wait(RecoveryAllowance), Is.True, "Acquire() never returned after the holder was abandoned.");
        }

        [Test]
        public void ReleasingFromADifferentThreadThanAcquiredSucceeds()
        {
            var name = $"Octopus.Calamari.CrossThreadRelease.{Guid.NewGuid():N}";
            var sut = new SystemSemaphoreManager();

            var releaser = sut.Acquire(name, "Another process is using the package journal");

            Exception releaseFailure = null;
            var releasingThread = new Thread(() =>
                                             {
                                                 try
                                                 {
                                                     releaser.Dispose();
                                                 }
                                                 catch (Exception ex)
                                                 {
                                                     releaseFailure = ex;
                                                 }
                                             });
            releasingThread.Start();

            Assert.That(releasingThread.Join(TimeSpan.FromSeconds(5)), Is.True, "Dispose() did not complete on the releasing thread.");
            Assert.That(releaseFailure, Is.Null, "Disposing the releaser from a different thread threw.");

            var reacquire = Task.Run(() =>
                                     {
                                         using (sut.Acquire(name, "Another process is using the package journal"))
                                         {
                                         }
                                     });

            Assert.That(reacquire.Wait(TimeSpan.FromSeconds(5)), Is.True, "Dispose() returned without actually releasing the mutex.");
        }
    }
}
