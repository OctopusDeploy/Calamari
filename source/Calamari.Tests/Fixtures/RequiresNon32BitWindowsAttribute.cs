using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Tests.Fixtures
{
    public class RequiresNon32BitWindowsAttribute : TestAttribute, ITestAction
    {
        public void BeforeTest(ITest testDetails)
        {
            if (CalamariEnvironment.IsRunningOnWindows && !Environment.Is64BitOperatingSystem)
            {
                Assert.Ignore($"This test does not run on 32Bit Windows");
            }
        }

        public void AfterTest(ITest testDetails)
        {
        }

        public ActionTargets Targets { get; set; }
    }
}