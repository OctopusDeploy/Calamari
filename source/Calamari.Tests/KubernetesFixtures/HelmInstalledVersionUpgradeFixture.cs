using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class HelmInstalledVersionUpgradeFixture : HelmUpgradeFixture
    {
        [Test]
        [TestPlatforms(TestPlatforms.Windows | TestPlatforms.Linux)]
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