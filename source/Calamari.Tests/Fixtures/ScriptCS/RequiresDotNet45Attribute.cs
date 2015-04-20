using System;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ScriptCS
{
    public class RequiresDotNet45Attribute : TestAttribute, ITestAction
    {
        public void BeforeTest(TestDetails testDetails)
        {
            if (!IsNet45OrNewer())
            {
                Assert.Ignore("Requires .NET 4.5");
            }
        }

        public void AfterTest(TestDetails testDetails)
        {
        }

        static bool IsNet45OrNewer()
        {
            // Class "ReflectionContext" exists from .NET 4.5 onwards.
            return Type.GetType("System.Reflection.ReflectionContext", false) != null;
        }

        public ActionTargets Targets { get; set; }
    }
}