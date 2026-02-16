using System.Security.Principal;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Testing.Requirements
{
    public class RequiresAdminAttribute : TestAttribute, ITestAction
    {
        public void BeforeTest(ITest testDetails)
        {
#pragma warning disable CA1416
            var isAdmin = (new WindowsPrincipal(WindowsIdentity.GetCurrent())).IsInRole(WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416
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
}
