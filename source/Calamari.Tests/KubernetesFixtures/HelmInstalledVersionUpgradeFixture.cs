using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class HelmInstalledVersionUpgradeFixture : HelmUpgradeFixture
    {
        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac("Helm is not installed on the Mac build agents")]
        [Category(TestCategory.PlatformAgnostic)]
        public void Upgrade_Succeeds()
        {
            var result = DeployPackage();

            result.AssertSuccess();
            result.AssertNoOutput("Using custom helm executable at");

            Assert.AreEqual(ReleaseName.ToLower(), result.CapturedOutput.OutputVariables["ReleaseName"]);
        }

        protected override string ExplicitExeVersion => null;
    }
}
