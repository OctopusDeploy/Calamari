using System;
using Calamari.Common.Plumbing;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Testing.Requirements;

public class RequiresWindowsServer2012OrAboveAttribute : TestAttribute, ITestAction
{
    public void BeforeTest(ITest testDetails)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2, 9200))
        {
            Assert.Ignore("Requires Windows Server 2012 or above");
        }
    }

    public void AfterTest(ITest testDetails)
    {
    }

    public ActionTargets Targets { get; set; }
}
