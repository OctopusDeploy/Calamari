using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Commands.Executors;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.Commands.Executors
{
    [TestFixture]
    public class AppliedResourcesOutputHelperTests
    {
        InMemoryLog log;

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
        }

        [Test]
        public void SetsAppliedResourcesOutputVariable_WhenFeatureToggleIsEnabled()
        {
            // Arrange
            var variables = new CalamariVariables
            {
                [KnownVariables.EnabledFeatureToggles] = OctopusFeatureToggles.KnownSlugs.ArgoRolloutsSupportFeatureToggle
            };
            var deployment = new RunningDeployment(variables);
            var resources = new[]
            {
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "my-deployment", "default"),
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.ServiceV1, "my-service", "default")
            };

            // Act
            AppliedResourcesOutputHelper.SetAppliedResourcesOutputVariable(log, deployment, resources);

            // Assert
            var outputVariable = variables.Get(SpecialVariables.AppliedResources);
            outputVariable.Should().NotBeNullOrEmpty();

            var deserializedResources = JsonConvert.DeserializeAnonymousType(outputVariable, new[]
            {
                new { Group = "", Version = "", Kind = "", Name = "", Namespace = "" }
            });

            deserializedResources.Should().HaveCount(2);
            deserializedResources[0].Should().BeEquivalentTo(new
            {
                Group = "apps",
                Version = "v1",
                Kind = "Deployment",
                Name = "my-deployment",
                Namespace = "default"
            });
            deserializedResources[1].Should().BeEquivalentTo(new
            {
                Group = "",
                Version = "v1",
                Kind = "Service",
                Name = "my-service",
                Namespace = "default"
            });
        }

        [Test]
        public void DoesNotSetOutputVariable_WhenFeatureToggleIsDisabled()
        {
            // Arrange
            var variables = new CalamariVariables();
            var deployment = new RunningDeployment(variables);
            var resources = new[]
            {
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "my-deployment", "default")
            };

            // Act
            AppliedResourcesOutputHelper.SetAppliedResourcesOutputVariable(log, deployment, resources);

            // Assert
            var outputVariable = variables.Get(SpecialVariables.AppliedResources);
            outputVariable.Should().BeNull();
        }

        [Test]
        public void HandlesEmptyResourceCollection_WhenFeatureToggleIsEnabled()
        {
            // Arrange
            var variables = new CalamariVariables
            {
                [KnownVariables.EnabledFeatureToggles] = OctopusFeatureToggles.KnownSlugs.ArgoRolloutsSupportFeatureToggle
            };
            var deployment = new RunningDeployment(variables);
            var resources = Enumerable.Empty<ResourceIdentifier>();

            // Act
            AppliedResourcesOutputHelper.SetAppliedResourcesOutputVariable(log, deployment, resources);

            // Assert
            var outputVariable = variables.Get(SpecialVariables.AppliedResources);
            outputVariable.Should().NotBeNullOrEmpty();
            outputVariable.Should().Be("[]");
        }

        [Test]
        public void SerializesResourcesWithCorrectJsonFormat()
        {
            // Arrange
            var variables = new CalamariVariables
            {
                [KnownVariables.EnabledFeatureToggles] = OctopusFeatureToggles.KnownSlugs.ArgoRolloutsSupportFeatureToggle
            };
            var deployment = new RunningDeployment(variables);
            var resources = new[]
            {
                new ResourceIdentifier(new ResourceGroupVersionKind("argoproj.io", "v1alpha1", "Rollout"), "my-rollout", "production")
            };

            // Act
            AppliedResourcesOutputHelper.SetAppliedResourcesOutputVariable(log, deployment, resources);

            // Assert
            var outputVariable = variables.Get(SpecialVariables.AppliedResources);
            var deserializedResources = JsonConvert.DeserializeAnonymousType(outputVariable, new[]
            {
                new { Group = "", Version = "", Kind = "", Name = "", Namespace = "" }
            });

            deserializedResources.Should().ContainSingle()
                .Which.Should().BeEquivalentTo(new
                {
                    Group = "argoproj.io",
                    Version = "v1alpha1",
                    Kind = "Rollout",
                    Name = "my-rollout",
                    Namespace = "production"
                });
        }
    }
}
