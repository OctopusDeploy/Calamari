using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using Calamari.Tests.Fixtures;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class Helm2UpgradeFixture : HelmUpgradeFixture
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
            result.AssertOutputMatches("STATUS: DEPLOYED");
            result.AssertOutputMatches(ConfigMapName);
            result.AssertOutputMatches($"release \"{ReleaseName}\" deleted");
            result.AssertOutput("Using custom helm executable at " + HelmExePath);

            Assert.AreEqual(ReleaseName.ToLower(), result.CapturedOutput.OutputVariables["ReleaseName"]);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        [Category(TestCategory.PlatformAgnostic)]
        public void TillerNamespace_CannotFindIfRandomNamespaceUsed()
        {   
            // We're basically just testing here that setting the tiller namespace does put the param into the cmd
            Variables.Set(Kubernetes.SpecialVariables.Helm.TillerNamespace, "random-foobar");

            var result = DeployPackage();
            result.AssertFailure();
            Log.StandardError.Should().ContainMatch("*Error: could not find tiller*");
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMac]
        [Category(TestCategory.PlatformAgnostic)]
        public void CustomHelmExeInPackage_RelativePath()
        {
            TestCustomHelmExeInPackage_RelativePath("2.9.0");
        }

        protected override string ExplicitExeVersion => "2.9.1";
    }
}
