using System;
using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.NamedLocks
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
    public class NixNamedLockFixture : NamedLockFixtureBase
    {
        [Test]
        public void NamedMutexesDefaultToGlobalScope()
        {
            // Without this, .NET 8.0.32+ creates user-scoped mutexes which fail when the uid has no passwd entry
            var isSet = AppContext.TryGetSwitch("System.Threading.Mutex.UnsafeDefaultMutexScopeGlobal", out var enabled);

            Assert.That(isSet && enabled, Is.True);
        }
    }
}