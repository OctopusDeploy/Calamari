using Calamari.Tests.Fixtures.Integration.Proxies;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Bash
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
    public class BashProxyFixture : ScriptProxyFixtureBase
    {
        protected override CalamariResult RunScript()
        {
            return RunScript("proxy.sh").result;
        }
    }
}