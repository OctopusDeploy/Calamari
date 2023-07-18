using System;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Fixtures.Integration.Proxies;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.DotnetScript
{
    [TestFixture]
    [Category(TestCategory.ScriptingSupport.DotnetScript)]
    [RequiresDotNetCore]
    public class DotnetScriptProxyFixture : WindowsScriptProxyFixtureBase
    {
        protected override CalamariResult RunScript()
        {
            ProxyUserName = "some@/user";
            ProxyPassword = "some@/password";
            UrlEncodedProxyUserName = "some%40%2Fuser";
            UrlEncodedProxyPassword = "some%40%2Fpassword";
            return RunScript("Proxy.csx").result;
        }

        protected override bool TestWebRequestDefaultProxy => true;
    }
}