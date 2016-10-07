using System;
using Calamari.Integration.Scripting;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures
{
    public class RequiresMinimumMonoVersionAttribute : TestAttribute, ITestAction
    {
        private readonly int major;
        private readonly int minor;
        private readonly int build;

        public RequiresMinimumMonoVersionAttribute(int major, int minor, int build)
        {
            this.major = major;
            this.minor = minor;
            this.build = build;
        }

        public void BeforeTest(TestDetails testDetails)
        {
            if (ScriptingEnvironment.IsRunningOnMono() && (ScriptingEnvironment.GetMonoVersion() < new Version(major, minor, build)))
            {
                Assert.Ignore($"Requires Mono {major}.{minor}.{build} or above");
            }
        }

        public void AfterTest(TestDetails testDetails)
        {
        }

        public ActionTargets Targets { get; set; }
    }
}