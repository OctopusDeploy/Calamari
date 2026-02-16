using System;
using System.Security.Principal;
using Calamari.Common.Plumbing;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Testing.Requirements
{
    public class RequiresAdminAttribute : TestAttribute, ITestAction
    {
        public void BeforeTest(ITest testDetails)
        {
            if (!OperatingSystem.IsWindows())  return;
            
            var isAdmin = (new WindowsPrincipal(WindowsIdentity.GetCurrent())).IsInRole(WindowsBuiltInRole.Administrator);
            if (!isAdmin)
            {
                Assert.Ignore("Requires Admin Rights");
            }
        }

        public void AfterTest(ITest testDetails)
        {
        }

        public ActionTargets Targets { get; set; }
    }

    public class RequiresWindowsServer2012OrAboveAttribute : TestAttribute, ITestAction
    {
        public void BeforeTest(ITest testDetails)
        {
            if (!CalamariEnvironment.IsRunningOnWindows)
            {
                Assert.Ignore("Requires Windows");
            }
        }

        public void AfterTest(ITest testDetails)
        {
        }

        public ActionTargets Targets { get; set; }
    }
}
