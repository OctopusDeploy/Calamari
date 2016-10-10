using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Calamari.Integration.Processes.Semaphores;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    [TestFixture]
    public class LockFileBasedSemaphoreCreatorFixture
    {
        [Test]
        public void ReturnsInstanceOfLockFileBasedSemaphore()
        {
            var creator = new LockFileBasedSemaphoreCreator();
            var result = creator.Create("name", TimeSpan.FromSeconds(1));
            Assert.That(result, Is.InstanceOf<LockFileBasedSemaphore>());
        }
    }
}
