using System;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    [TestFixture]
    public class KubernetesYamlTests
    {
        [Test]
        public void ShouldGenerateCorrectIdentifiers()
        {
            var input = TestFileLoader.Load("single-deployment.yaml");
            var got = KubernetesYaml.GetDefinedResources(new[] { input }, string.Empty);
            var expected = new[]
            {
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1,
                                       "nginx",
                                       "test"
                                      )
            };

            got.Should().BeEquivalentTo(expected);
        }

        [Test]
        public void ShouldOmitDefinitionIfTheMetadataSectionIsNotSet()
        {
            var input = TestFileLoader.Load("invalid.yaml");
            var got = KubernetesYaml.GetDefinedResources(new[] { input }, string.Empty);
            got.Should().BeEmpty();
        }

        [Test]
        public void ShouldHandleMultipleResourcesDefinedInTheSameFile()
        {
            var input = TestFileLoader.Load("multiple-resources.yaml");
            var got = KubernetesYaml.GetDefinedResources(new[] { input }, string.Empty);
            var expected = new[]
            {
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "nginx", "default"),
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.ConfigMapV1, "config", "default"),
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.PodV1, "curl", "default")
            };

            got.Should().BeEquivalentTo(expected);
        }

        [Test]
        public void ShouldUseDefaultNamespaceWhenNoNamespaceIsSuppliedInYaml()
        {
            const string defaultNamespace = "DefaultNamespace";
            var input = TestFileLoader.Load("no-namespace.yaml");
            var got = KubernetesYaml.GetDefinedResources(new[] { input }, defaultNamespace);
            var expected = new[]
            {
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1,
                                       "nginx",
                                       defaultNamespace
                                      )
            };

            got.Should().BeEquivalentTo(expected);
        }

        [Test]
        public void ShouldHandleMultipleYamlFiles()
        {
            var manifest = TestFileLoader.Load("single-deployment.yaml");
            var multipleFileInput = new[] { manifest, manifest };
            var got = KubernetesYaml.GetDefinedResources(multipleFileInput, string.Empty);
            var expected = new[]
            {
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1,
                                       "nginx",
                                       "test"),
                new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1,
                                       "nginx",
                                       "test")
            };

            got.Should().BeEquivalentTo(expected);
        }
    }
}