using System.Collections.Generic;
using System.Threading;
using Calamari.Integration.Processes;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Processes
{
    [TestFixture]
    public class SemaphoreFixture
    {
        [Test]
        public void ShouldIsolate()
        {
            var semaphore = new SystemSemaphore();

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
    }
}
