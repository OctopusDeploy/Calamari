using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
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

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public void ReportsObjectStatus()
        {
            Variables.AddFlag(SpecialVariables.ResourceStatusCheck, true);
            Variables.Set(KnownVariables.EnabledFeatureToggles, OctopusFeatureToggles.KnownSlugs.KOSForHelm);
            Variables.Set(SpecialVariables.Helm.Timeout, "2m30s");

            var result = DeployPackage();

            result.AssertSuccess();
            result.AssertOutputMatches($"NAMESPACE: {Namespace}");
            result.AssertOutputMatches("STATUS: deployed");
            result.AssertOutputMatches($"release \"{ReleaseName}\" uninstalled");
            result.AssertOutput("Using custom helm executable at " + HelmExePath);

            Assert.AreEqual(ReleaseName.ToLower(), result.CapturedOutput.OutputVariables["ReleaseName"]);

            result.AssertOutputMatches("Resource Status Check: Performing resource status checks on the following resources:");
            result.AssertOutputContains("- ConfigMap/Thing");
            result.AssertOutputMatches("Resource Status Check: Stopping after next status check.");
            result.AssertOutputMatches("Resource Status Check: reported 1 updates, 0 removals");
            result.AssertOutputMatches("Resource Status Check: Stopped.");

            result.AssertServiceMessage(SpecialVariables.ServiceMessageNames.ResourceStatus.Name,
                                        Is.True,
                                        properties: new Dictionary<string, object>
                                        {
                                            ["name"] = "",
                                            ["checkCount"] = 1
                                        });
        }

        protected override string ExplicitExeVersion => "3.16.2";
    }
}