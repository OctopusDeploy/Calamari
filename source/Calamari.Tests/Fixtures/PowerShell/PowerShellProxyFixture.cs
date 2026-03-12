using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.Proxies;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PowerShell
{
    [TestFixture]
    [TestPlatforms(TestPlatforms.Windows)]
    public class PowerShellProxyFixture : WindowsScriptProxyFixtureBase
    {
        protected override CalamariResult RunScript()
        {
            return RunScript("Proxy.ps1").result;
        }

        protected override bool TestWebRequestDefaultProxy => true;
    }
}