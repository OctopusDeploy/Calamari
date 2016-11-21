using Calamari.Integration.Processes.Semaphores;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    [TestFixture]
    public class SemaphoreFactoryFixture
    {
        [Test]
        [RequiresMono]
        public void ReturnsFileBasedSemaphoreManagerForMono()
        {
            if (!CalamariEnvironment.IsRunningOnMono)
                Assert.Ignore("This test is designed to run on mono");
            var result = SemaphoreFactory.Get();
            Assert.That(result, Is.InstanceOf<FileBasedSempahoreManager>());
        }

#if NET40
        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
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