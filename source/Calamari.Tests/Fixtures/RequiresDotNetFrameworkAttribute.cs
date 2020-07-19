using Calamari.Common.Plumbing.Extensions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Tests.Fixtures
{
    public class RequiresDotNetFrameworkAttribute : TestAttribute, ITestAction
    {
        public void BeforeTest(ITest testDetails)
        {
            if (!ScriptingEnvironment.IsNetFramework())
            {
                Assert.Ignore("Requires .NET Framework");
            }
        }

        public void AfterTest(ITest testDetails)
        {
        }

        public ActionTargets Targets { get; set; }
    }
}