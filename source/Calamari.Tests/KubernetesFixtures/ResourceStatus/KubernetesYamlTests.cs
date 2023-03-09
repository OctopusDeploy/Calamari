using System.Linq;
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
            var got = KubernetesYaml.GetDefinedResources(input);
            got.Should().HaveCount(1);
            var identifier = got.First();
            identifier.Kind.Should().Be("Deployment");
            identifier.Name.Should().Be("nginx");
            identifier.Namespace.Should().Be("test");
        }

        [Test]
        public void ShouldHandleMultipleResourcesDefinedInTheSameFile()
        {
            var input = TestFileLoader.Load("multiple-resources.yaml");
            var got = KubernetesYaml.GetDefinedResources(input);
            got.Should().HaveCount(3);
            
            got.FirstOrDefault(id => id.Kind == "Deployment")
                .Should().BeEquivalentTo(new ResourceIdentifier("Deployment", "nginx", "default"));
            got.FirstOrDefault(id => id.Kind == "ConfigMap")
                .Should().BeEquivalentTo(new ResourceIdentifier("ConfigMap", "config", "default"));
            got.FirstOrDefault(id => id.Kind == "Pod")
                .Should().BeEquivalentTo(new ResourceIdentifier("Pod", "curl", "default"));
        }
    }
}