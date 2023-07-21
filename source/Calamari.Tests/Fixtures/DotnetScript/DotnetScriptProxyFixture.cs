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
            return RunScript("Proxy.csx").result;
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public override void Initialize_HasSystemProxy_UseSystemProxyWithExceptions()
        {
            // If the HTTP_PROXY environment variable have been set .NET Core will only take entries in the NO_PROXY environment variable into account,
            // it will not look at Exceptions listed under Proxy Settings in Internet Options on Windows.
            // As the NO_PROXY environment variable is set to a fixed list of addresses, the Initialize_HasSystemProxy_UseSystemProxyWithExceptions test fails
            // as the address we want to bypass the proxy doesn't isn't configured. 
            Assert.Ignore("Some proxy tests currently fail with dotnet-script, currently ignoring them until this has been addressed.");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public override void Initialize_HasSystemProxy_UseSystemProxyWithCredentials()
        {
            // .NET Core first unescapes the username and password, then splits the auth string on ':' and finally escapes the username and password again.
            // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/HttpEnvironmentProxy.cs#L175C18-L175C18
            Assert.Ignore(".NET Core currently does weird things when proxy username contains ':', currently ignoring this until this has been addressed.");
        }

        protected override bool TestWebRequestDefaultProxy => true;
    }
}