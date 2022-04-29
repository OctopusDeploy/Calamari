using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.Proxies;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.FSharp
{
    [TestFixture]
    [Category(TestCategory.ScriptingSupport.FSharp)]
    public class FSharpProxyFixture : WindowsScriptProxyFixtureBase
    {
        protected override CalamariResult RunScript()
        {
            return RunScript("Proxy.fsx").result;
        }

        protected override bool TestWebRequestDefaultProxy => true;
    }
}