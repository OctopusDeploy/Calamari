using System;
using System.Net;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Integration.Proxies;
using Calamari.Tests.Fixtures.PowerShell;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ScriptCS
{
    [TestFixture]
    [Category(TestCategory.ScriptingSupport.ScriptCS)]
    public class ScriptCSProxyFixture : WindowsScriptProxyFixtureBase
    {
        protected override CalamariResult RunScript()
        {
            return RunScript("Proxy.csx").result;
        }

        protected override bool TestWebRequestDefaultProxy => true;
    }
}