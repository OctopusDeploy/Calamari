using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes.Semaphores;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    public abstract class SemaphoreFixtureBase
    {
        // Must comfortably exceed the 3s initial wait inside SystemSemaphoreManager.
        static readonly TimeSpan RecoveryAllowance = TimeSpan.FromSeconds(15);

        // Held so the abandoned mutex handle is never closed or finalised during the test
        Mutex abandonedMutex;

        [TearDown]
        public void DropAbandonedMutex()
        {
            abandonedMutex = null;
        }

        [Test]
        public void SystemSemaphoreWaitsUntilFirstSemaphoreIsReleased()
        {
            SecondSemaphoreWaitsUntilFirstSemaphoreIsReleased(new SystemSemaphoreManager());
        }

        [Test]
        public void SystemSemaphoreShouldIsolate()
        {
            ShouldIsolate(new SystemSemaphoreManager());
        }

        // Acquire() must recover when the holder dies without running the Releaser (killed mid-ApplyRetention,
        // Tentacle restart, OOM).
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

        [Test]
        public void DisposingTheReleaserTwiceIsANoOp()
        {
            var name = $"Octopus.Calamari.DoubleDispose.{Guid.NewGuid():N}";
            var sut = new SystemSemaphoreManager();

            var releaser = sut.Acquire(name, "Another process is using the package journal");
            releaser.Dispose();

            Assert.DoesNotThrow(() => releaser.Dispose(), "Disposing the releaser a second time threw.");

            var reacquire = Task.Run(() =>
                                     {
                                         using (sut.Acquire(name, "Another process is using the package journal"))
                                         {
                                         }
                                     });

            Assert.That(reacquire.Wait(TimeSpan.FromSeconds(5)), Is.True, "The second Dispose() interfered with the released mutex.");
        }

        static void ShouldIsolate(ISemaphoreFactory semaphore)
        {
            var result = 0;
            var threads = new List<Thread>();

            for (var i = 0; i < 4; i++)
            {
                threads.Add(new Thread(new ThreadStart(delegate
                {
                    using (semaphore.Acquire("CalamariTest", "Another process is performing arithmetic, please wait"))
                    {
                        result = 1;
                        Thread.Sleep(200);
                        result = result + 1;
                        Thread.Sleep(200);
                        result = result + 1;
                    }
                })));
            }

            foreach (var thread in threads)
                thread.Start();

            foreach (var thread in threads)
                thread.Join();

            Assert.That(result, Is.EqualTo(3));
        }

        static void SecondSemaphoreWaitsUntilFirstSemaphoreIsReleased(ISemaphoreFactory semaphore)
        {
            AutoResetEvent autoEvent = new AutoResetEvent(false);
            var threadTwoShouldGetSemaphore = true;

            var threadOne = new Thread(() =>
            {
                using (semaphore.Acquire("Octopus.Calamari.TestSemaphore", "Another process has the semaphore..."))
                {
                    threadTwoShouldGetSemaphore = false;
                    autoEvent.Set();
                    Thread.Sleep(200);
                    threadTwoShouldGetSemaphore = true;
                }
            });

            var threadTwo = new Thread(() =>
            {
                autoEvent.WaitOne();
                using (semaphore.Acquire("Octopus.Calamari.TestSemaphore", "Another process has the semaphore..."))
                {
                    Assert.That(threadTwoShouldGetSemaphore, Is.True);
                }
            });

            threadOne.Start();
            threadTwo.Start();
            threadOne.Join();
            threadTwo.Join();
        }
    }
}
