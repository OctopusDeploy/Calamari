using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Testing.Requirements;

public class RequiresWindowsServer2016OrAboveAttribute : TestAttribute, ITestAction
{
    readonly string reason;

    public RequiresWindowsServer2016OrAboveAttribute(string reason)
    {
        this.reason = reason;
    }

    public void BeforeTest(ITest testDetails)
    {
<<<<<<< HEAD
=======
#if NET8
>>>>>>> release/2025.4
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
        {
            Assert.Ignore("Requires Windows Server 2016 or above: " + reason);
        }
<<<<<<< HEAD
=======
#else
        if(Environment.OSVersion.Version < new Version(10, 0, 14393))
        {
            Assert.Ignore("Requires Windows Server 2012 or above: " + reason);
        }
#endif
>>>>>>> release/2025.4
    }

    public void AfterTest(ITest testDetails)
    {
    }

    public ActionTargets Targets { get; set; }
}
