using System;
using Calamari.Common.Plumbing.Extensions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Testing.Requirements
{
    public class RequiresMonoVersionBefore : TestAttribute, ITestAction
    {
        private readonly int major;
        private readonly int minor;
        private readonly int build;

        public RequiresMonoVersionBefore(int major, int minor, int build)
        {
            this.major = major;
            this.minor = minor;
            this.build = build;
        }

        public void BeforeTest(ITest testDetails)
        {
            if (ScriptingEnvironment.IsRunningOnMono() && (ScriptingEnvironment.GetMonoVersion() >= new Version(major, minor, build)))
            {
                Assert.Ignore($"Requires a Mono version before {major}.{minor}.{build}");
            }
        }

        public void AfterTest(ITest testDetails)
        {
        }

        public ActionTargets Targets { get; set; }
    }
}