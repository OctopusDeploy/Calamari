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
            var got = KubernetesYaml.GetDefinedResources(input, string.Empty);
            var expected = new ResourceIdentifier[]
            {
                new ResourceIdentifier(
                    "Deployment",
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
            var got = KubernetesYaml.GetDefinedResources(input, string.Empty);
            got.Should().BeEmpty();
        }
        
        [Test]
        public void ShouldHandleMultipleResourcesDefinedInTheSameFile()
        {
            var input = TestFileLoader.Load("multiple-resources.yaml");
            var got = KubernetesYaml.GetDefinedResources(input, string.Empty);
            var expected = new ResourceIdentifier[]
            {
                new ResourceIdentifier("Deployment", "nginx", "default"),
                new ResourceIdentifier("ConfigMap", "config", "default"),
                new ResourceIdentifier("Pod", "curl", "default")
            };

            got.Should().BeEquivalentTo(expected);
        }

        [Test]
        public void ShouldUseDefaultNamespaceWhenNoNamespaceIsSuppliedInYaml()
        {
            const string defaultNamespace = "DefaultNamespace";
            var input = TestFileLoader.Load("no-namespace.yaml");
            var got = KubernetesYaml.GetDefinedResources(input, defaultNamespace);
            var expected = new ResourceIdentifier[]
            {
                new ResourceIdentifier(
                    "Deployment",
                    "nginx",
                    defaultNamespace
                )
            };

            got.Should().BeEquivalentTo(expected);
        }
    }
}