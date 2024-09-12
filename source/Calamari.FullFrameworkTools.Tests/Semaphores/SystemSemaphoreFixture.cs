using System;
using Calamari.FullFrameworkTools.WindowsCertStore.Semaphores;
using NUnit.Framework;

namespace Calamari.FullFrameworkTools.Tests.Semaphores
{
    [TestFixture]
    public class SystemSemaphoreFixture : SemaphoreFixtureBase
    {
        [Test]
        public void SystemSemaphoreWaitsUntilFirstSemaphoreIsReleased()
        {
            SecondSemaphoreWaitsUntilFirstSemaphoreIsReleased(new SystemSemaphoreManager(null));
        }

        [Test]
        public void SystemSemaphoreShouldIsolate()
        {
            ShouldIsolate(new SystemSemaphoreManager(null));
        }
    }
}