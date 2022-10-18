using System.Threading.Tasks;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Fixtures;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class Helm3UpgradeFixture : HelmUpgradeFixture
    {
        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public void Upgrade_Succeeds()
        {
            var result = DeployPackage();

            result.AssertSuccess();
            result.AssertOutputMatches($"NAMESPACE: {Namespace}");
            result.AssertOutputMatches("STATUS: deployed");
            result.AssertOutputMatches($"release \"{ReleaseName}\" uninstalled");
            result.AssertOutput("Using custom helm executable at " + HelmExePath);

            Assert.AreEqual(ReleaseName.ToLower(), result.CapturedOutput.OutputVariables["ReleaseName"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public async Task CustomHelmExeInPackage_RelativePath()
        {
            await TestCustomHelmExeInPackage_RelativePath("3.0.1");
        }

        protected override string ExplicitExeVersion => "3.0.2";
    }
}