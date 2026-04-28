using System;
using System.Security.Principal;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Testing.Requirements
{
    public class RequiresWindowsAdminAttribute : TestAttribute, ITestAction
    {
        public void BeforeTest(ITest testDetails)
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Fail("This can only be run on Windows");
                return;
            }
            
            var isAdmin = (new WindowsPrincipal(WindowsIdentity.GetCurrent())).IsInRole(WindowsBuiltInRole.Administrator);
            if (!isAdmin)
            {
                Assert.Ignore("Requires Admin rights");
            }
        }

        public void AfterTest(ITest testDetails)
        {
        }

        public ActionTargets Targets { get; set; }
    }
}
