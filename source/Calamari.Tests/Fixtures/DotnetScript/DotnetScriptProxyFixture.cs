using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Variables;
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
            return RunScript("Proxy.csx", new Dictionary<string, string>() {{ ScriptVariables.UseDotnetScript, "true" }}).result;
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
            // According to this issue https://github.com/dotnet/runtime/issues/23132 this is expected behavior as per the
            // HTTP Authentication: Basic and Digest Access Authentication RFC (https://datatracker.ietf.org/doc/html/rfc2617#section-2) auth does not 
            // support ':' in domain.
            Assert.Ignore(".NET Core currently does weird things when proxy username contains ':'.");
        }

        protected override bool TestWebRequestDefaultProxy => true;
    }
}