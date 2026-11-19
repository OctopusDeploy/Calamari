using System;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Process.NamedLocks
{
    [TestFixture]
    [TestPlatforms(TestPlatforms.Unix)]
    public class NixNamedLockFixture : NamedLockFixtureBase
    {
    }
}
