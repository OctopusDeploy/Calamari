using NUnit.Framework;

namespace Calamari.Tests.Helpers
{
    public class PlatformTestAttribute : TestAttribute, ITestAction {
        private readonly CompatablePlatform requiredPlatform;

        public PlatformTestAttribute(CompatablePlatform requiredPlatform)
        {
            this.requiredPlatform = requiredPlatform;
        }

        public void BeforeTest(TestDetails testDetails)
        {
            
            if (requiredPlatform == CompatablePlatform.Nix && !CalamariEnvironment.IsRunningOnNix)
            {
                Assert.Ignore("This test only runs on *Nix machines");
            }

            if (requiredPlatform == CompatablePlatform.Windows && CalamariEnvironment.IsRunningOnNix)
            {
                Assert.Ignore("This test only runs on Windows machines");
            }
        }

        public void AfterTest(TestDetails testDetails)
        {
        }

        public ActionTargets Targets { get; }
    }

    public enum CompatablePlatform
    {
        Nix,
        Windows
    }
}