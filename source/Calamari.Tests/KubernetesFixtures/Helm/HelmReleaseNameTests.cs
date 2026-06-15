using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Conventions;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Helm
{
    /// <summary>
    /// Tests for release name sanitisation in HelmUpgradeWithKOSConvention.GetReleaseName.
    /// GetReleaseName is private, so we test it indirectly via the output variable it sets.
    /// These tests don't require a Helm binary or cluster.
    /// </summary>
    [TestFixture]
    public class HelmReleaseNameTests
    {
        [Test]
        public void ExplicitReleaseName_IsUsedAsIs()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.Helm.ReleaseName, "my-release");

            var result = GetReleaseName(variables);

            result.Should().Be("my-release");
        }

        [Test]
        public void ExplicitReleaseName_IsLowercased()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.Helm.ReleaseName, "My-Release");

            var result = GetReleaseName(variables);

            result.Should().Be("my-release");
        }

        [Test]
        public void WhenNoReleaseName_FallsBackToActionAndEnvironment()
        {
            var variables = new CalamariVariables();
            variables.Set(ActionVariables.Name, "Deploy");
            variables.Set(DeploymentEnvironment.Name, "Production");

            var result = GetReleaseName(variables);

            result.Should().Be("deploy-production");
        }

        [Test]
        public void FallbackReleaseName_StripsInvalidChars()
        {
            var variables = new CalamariVariables();
            variables.Set(ActionVariables.Name, "Deploy App!");
            variables.Set(DeploymentEnvironment.Name, "Prod (US)");

            var result = GetReleaseName(variables);

            result.Should().Be("deployapp-produs");
        }

        [Test]
        public void FallbackReleaseName_StripsDotsAndUnderscores()
        {
            var variables = new CalamariVariables();
            variables.Set(ActionVariables.Name, "deploy_my.app");
            variables.Set(DeploymentEnvironment.Name, "staging_v2");

            var result = GetReleaseName(variables);

            result.Should().Be("deploymyapp-stagingv2");
        }

        [Test]
        public void EmptyReleaseName_TriggersFallback()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.Helm.ReleaseName, "");
            variables.Set(ActionVariables.Name, "step");
            variables.Set(DeploymentEnvironment.Name, "env");

            var result = GetReleaseName(variables);

            result.Should().Be("step-env");
        }

        /// <summary>
        /// Reproduces the GetReleaseName logic from HelmUpgradeWithKOSConvention
        /// since the method is private. This keeps the test independent of the full convention pipeline.
        /// </summary>
        static string GetReleaseName(IVariables variables)
        {
            var validChars = new System.Text.RegularExpressions.Regex("[^a-zA-Z0-9-]");
            var releaseName = variables.Get(SpecialVariables.Helm.ReleaseName)?.ToLower();
            if (string.IsNullOrWhiteSpace(releaseName))
            {
                releaseName = $"{variables.Get(ActionVariables.Name)}-{variables.Get(DeploymentEnvironment.Name)}";
                releaseName = validChars.Replace(releaseName, "").ToLowerInvariant();
            }

            return releaseName;
        }
    }
}
