using System;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.Proxies;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.DotnetScript
{
    [TestFixture]
    [Category(TestCategory.ScriptingSupport.DotnetScript)]
    public class DotnetScriptProxyFixture : WindowsScriptProxyFixtureBase
    {
        protected override CalamariResult RunScript()
        {
            return RunScript("Proxy.csx").result;
        }

        protected override bool TestWebRequestDefaultProxy => true;
    }
}