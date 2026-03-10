using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.Proxies;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Bash
{
    [TestFixture]
    [TestPlatforms(TestPlatforms.Unix)]
    public class BashProxyFixture : ScriptProxyFixtureBase
    {
        protected override CalamariResult RunScript()
        {
            return RunScript("proxy.sh").result;
        }
    }
}