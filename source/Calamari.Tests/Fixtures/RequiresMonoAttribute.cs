using Calamari.Common.Plumbing;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Calamari.Tests.Fixtures
{
    public class RequiresMonoAttribute : TestAttribute, ITestAction
    {
        public void BeforeTest(ITest test)
        {
            if (!CalamariEnvironment.IsRunningOnMono)
                Assert.Ignore("This test is designed to run on mono");
        }

        public void AfterTest(ITest test)
        {
        }

        public ActionTargets Targets { get; }
    }
}