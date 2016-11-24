using Calamari.Integration.Scripting;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Tests.Integration.Fixtures
{
    public class RequiresDotNet45Attribute : TestAttribute, ITestAction
    {
        public void BeforeTest(ITest testDetails)
        {
            if (!ScriptingEnvironment.IsNet45OrNewer())
            {
                Assert.Ignore("Requires .NET 4.5");
            }
        }

        public void AfterTest(ITest testDetails)
        {
        }

        public ActionTargets Targets { get; set; }
    }
}