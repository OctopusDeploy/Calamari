using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.NamedLocks
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class WindowsNamedLockFixture : NamedLockFixtureBase
    { }
}
