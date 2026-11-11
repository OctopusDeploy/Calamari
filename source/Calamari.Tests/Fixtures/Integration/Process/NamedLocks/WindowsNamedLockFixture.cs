using Calamari.Testing;
using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.NamedLocks
{
    [TestFixture]
    [TestPlatforms(TestPlatforms.Windows)]
    public class WindowsNamedLockFixture : NamedLockFixtureBase
    { }
}
