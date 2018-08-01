using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Tests.Fixtures
{
    public class RequiresNon32BitWindowsAttribute : TestAttribute, ITestAction
    {
        public void BeforeTest(ITest testDetails)
        {
            if (CalamariEnvironment.IsRunningOnWindows && (Environment.GetEnvironmentVariable("teamcity.agent.jvm.os.arch")?.Contains("x86") ?? false))
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