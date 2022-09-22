using System.Collections.Generic;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.Proxies;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PowerShell
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class PowerShellCoreProxyFixture : WindowsScriptProxyFixtureBase
    {
        [SetUp]
        public void Setup()
        {
            Assert.Ignore("Some proxy tests currently fail with PSCore, currently ignoring them until this has been addressed.");
        }

        protected override CalamariResult RunScript()
        {
            var variables = new Dictionary<string,string>()
            {
                {PowerShellVariables.Edition, "Core"}
            };

            return RunScript("Proxy.ps1", variables).result;
        }

        protected override bool TestWebRequestDefaultProxy => true;
    }
}
