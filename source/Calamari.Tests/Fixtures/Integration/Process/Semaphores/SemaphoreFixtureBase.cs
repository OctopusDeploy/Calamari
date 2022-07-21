using System;
using System.Collections.Generic;
using System.Threading;
using Calamari.Common.Features.Processes.Semaphores;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    public abstract class SemaphoreFixtureBase
    {
        protected void ShouldIsolate(ISemaphoreFactory semaphore)
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

        protected void SecondSemaphoreWaitsUntilFirstSemaphoreIsReleased(ISemaphoreFactory semaphore)
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
