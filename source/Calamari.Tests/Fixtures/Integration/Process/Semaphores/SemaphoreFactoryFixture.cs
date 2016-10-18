using Calamari.Integration.Processes.Semaphores;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    [TestFixture]
    public class SemaphoreFactoryFixture
    {
        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)] //todo: add macOS here
        public void ReturnsFileBasedSemaphoreManagerForLinux()
        {
            if (!CalamariEnvironment.IsRunningOnNix)
                Assert.Ignore("This test is designed to run on *nix");
            var result = SemaphoreFactory.Get();
            Assert.That(result, Is.InstanceOf<FileBasedSempahoreManager>());
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ReturnsSystemSemaphoreManagerForWindows()
        {
            if (!CalamariEnvironment.IsRunningOnWindows)
                Assert.Ignore("This test is designed to run on windows");
            var result = SemaphoreFactory.Get();
            Assert.That(result, Is.InstanceOf<SystemSemaphoreManager>());
        }
    }
}