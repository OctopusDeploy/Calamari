using System.Security.Principal;
using Calamari.Integration.Scripting;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Tests.Fixtures
{
    public class RequiresAdminAttribute : TestAttribute, ITestAction
    {
        public void BeforeTest(ITest testDetails)
        {
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
}