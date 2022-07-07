using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    [TestFixture]
    public class SemaphoreFactoryFixture
    {
#if NETFX
        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ReturnsSystemSemaphoreManagerForWindows()
        {
            if (!CalamariEnvironment.IsRunningOnWindows)
                Assert.Ignore("This test is designed to run on windows");
            var result = SemaphoreFactory.Get();
            Assert.That(result, Is.InstanceOf<SystemSemaphoreManager>());
        }
#else
        [Test]
        public void ReturnsSystemSemaphoreManagerForAllPlatformsUnderNetCore()
        {
            var result = SemaphoreFactory.Get();
            Assert.That(result, Is.InstanceOf<SystemSemaphoreManager>());
        }
#endif
    }
}