using System;
using Calamari.Integration.Scripting;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Tests.Fixtures
{
    public class RequiresNonFreeBSDPlatformAttribute : TestAttribute, ITestAction
    {
        public void BeforeTest(ITest testDetails)
        {
            if (ScriptingEnvironment.IsRunningOnMono() && (Environment.GetEnvironmentVariable("TEAMCITY_BUILDCONF_NAME")?.Contains("FreeBSD") ?? false))
            {
                Assert.Ignore($"This test does not run on FreeBSD");
            }
        }

        public void AfterTest(ITest testDetails)
        {
        }

        public ActionTargets Targets { get; set; }
    }
}