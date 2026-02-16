using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Testing.Requirements;

public class RequiresWindowsServer2016OrAboveAttribute(string reason) : TestAttribute, ITestAction
{
    public void BeforeTest(ITest testDetails)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
        {
            Assert.Ignore("Requires Windows Server 2016 or above");
        }
    }

    public void AfterTest(ITest testDetails)
    {
    }

    public ActionTargets Targets { get; set; }
}
