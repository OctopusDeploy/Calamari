using System;
using Calamari.Common.Plumbing;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Testing.Requirements;

public class RequiresWindowsServer2012OrAboveAttribute : TestAttribute, ITestAction
{
    public void BeforeTest(ITest testDetails)
    {
<<<<<<< HEAD
=======
#if NET8
>>>>>>> release/2025.4
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2, 9200))
        {
            Assert.Ignore("Requires Windows Server 2012 or above");
        }
<<<<<<< HEAD
=======
#elif NETFX
        var decimalVersion = Environment.OSVersion.Version.Major + Environment.OSVersion.Version.Minor * 0.1;
        if(decimalVersion < 6.2)
        {
            Assert.Ignore("Requires Windows Server 2012 or above");
        }
#else
        // .NET Core will be new enough.
#endif
>>>>>>> release/2025.4
    }

    public void AfterTest(ITest testDetails)
    {
    }

    public ActionTargets Targets { get; set; }
}
