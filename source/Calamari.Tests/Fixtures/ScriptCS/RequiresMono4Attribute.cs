using System;
using Calamari.Integration.Scripting.ScriptCS;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ScriptCS
{
    public class RequiresMono4Attribute : TestAttribute, ITestAction
    {
        public void BeforeTest(TestDetails testDetails)
        {
            if (ScriptingEnvironment.IsRunningOnMono() && (ScriptingEnvironment.GetMonoVersion() < new Version(4,0,0)))
            {
                Assert.Ignore("Requires Mono 4");
            }
        }

        public void AfterTest(TestDetails testDetails)
        {
        }

        public ActionTargets Targets { get; set; }
    }
}