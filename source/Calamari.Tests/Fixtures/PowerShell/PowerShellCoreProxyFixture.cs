using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Tests.Fixtures.Integration.Proxies;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PowerShell
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class PowerShellCoreProxyFixture : WindowsScriptProxyFixtureBase
    {
        protected override CalamariResult RunScript()
        {
            var variables = new Dictionary<string,string>()
            {
                {SpecialVariables.Action.PowerShell.Edition, "Core"}
            };

            return RunScript("Proxy.ps1", variables).result;
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public override void Initialize_HasSystemProxy_UseSystemProxyWithCredentials()
        {
            Assert.Inconclusive("This test currently fails on PSCore");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public override void Initialize_HasSystemProxy_UseSystemProxyWithExceptions()
        {
            Assert.Inconclusive("This test currently fails on PSCore");
        }

        protected override bool TestWebRequestDefaultProxy => true;
    }
}
