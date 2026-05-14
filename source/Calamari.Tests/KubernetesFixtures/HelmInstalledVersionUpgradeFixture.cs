using System.Threading.Tasks;
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
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public async Task Upgrade_Succeeds()
        {
            var result =await  DeployPackage();

            result.AssertSuccess();
            result.AssertNoOutput("Using custom helm executable at");

            Assert.AreEqual(ReleaseName.ToLower(), result.CapturedOutput.OutputVariables["ReleaseName"]);
        }

        protected override string ExplicitExeVersion => null;
    }
}