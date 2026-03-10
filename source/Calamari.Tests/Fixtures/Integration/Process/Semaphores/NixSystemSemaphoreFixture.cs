using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.Semaphores
{
    [TestFixture]
    [TestPlatforms(TestPlatforms.Unix)]
    public class NixSystemSemaphoreFixture : SemaphoreFixtureBase
    {
    }
}