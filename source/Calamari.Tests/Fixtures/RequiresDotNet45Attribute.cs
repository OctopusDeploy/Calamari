using System;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures
{
    public class RequiresDotNet45Attribute : TestAttribute, ITestAction
    {
        public void BeforeTest(TestDetails testDetails)
        {
            if (!ScriptingEnvironment.IsNet45OrNewer())
            {
                Assert.Ignore("Requires .NET 4.5");
            }
        }

        public void AfterTest(TestDetails testDetails)
        {
        }

        public ActionTargets Targets { get; set; }
    }
}