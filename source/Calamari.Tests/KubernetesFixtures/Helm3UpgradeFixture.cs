using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Fixtures;
using Calamari.Tests.Helpers;
using FluentAssertions;
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
            result.AssertOutputMatches($"- v1/ConfigMap/mychart-configmap-{ReleaseName}");
            result.AssertOutputMatches("Resource Status Check: Stopping after next status check.");
            result.AssertOutputMatches("Resource Status Check: reported 1 updates, 0 removals");
            result.AssertOutputMatches("Resource Status Check: Stopped.");

            result.CapturedOutput.ServiceMessages
                  .Where(sm => sm.Name == SpecialVariables.ServiceMessages.ResourceStatus.Name)
                  .Should()
                  .HaveCountGreaterOrEqualTo(1);

            result.CapturedOutput.ServiceMessages
                  .Where(sm => sm.Name == SpecialVariables.ServiceMessages.ResourceStatus.Name)
                  .Should()
                  .Contain(sm => sm.Properties[SpecialVariables.ServiceMessages.ResourceStatus.Attributes.Name] == $"mychart-configmap-{ReleaseName}" && 
                                 sm.Properties[SpecialVariables.ServiceMessages.ResourceStatus.Attributes.Namespace] == Namespace && 
                                 sm.Properties[SpecialVariables.ServiceMessages.ResourceStatus.Attributes.Group] == "" &&
                                 sm.Properties[SpecialVariables.ServiceMessages.ResourceStatus.Attributes.Version] == "v1" &&
                                 sm.Properties[SpecialVariables.ServiceMessages.ResourceStatus.Attributes.Kind] == "ConfigMap" &&
                                 sm.Properties[SpecialVariables.ServiceMessages.ResourceStatus.Attributes.Status] == Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful.ToString());
        }

        protected override string ExplicitExeVersion => "3.16.2";
    }
}