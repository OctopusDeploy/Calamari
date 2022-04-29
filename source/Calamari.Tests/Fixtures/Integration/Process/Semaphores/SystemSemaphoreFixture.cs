using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    [TestFixture]
    public class SystemSemaphoreFixture : SemaphoreFixtureBase
    {
        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void SystemSemaphoreWaitsUntilFirstSemaphoreIsReleased()
        {
            SecondSemaphoreWaitsUntilFirstSemaphoreIsReleased(new SystemSemaphoreManager());
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void SystemSemaphoreShouldIsolate()
        {
            ShouldIsolate(new SystemSemaphoreManager());
        }
    }
}