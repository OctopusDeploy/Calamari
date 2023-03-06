using System.Linq;
using Calamari.Kubernetes.ResourceStatus;
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
            var input = ResourceLoader.Load("single-deployment.yaml");
            var got = KubernetesYaml.GetDefinedResources(input);
            got.Should().HaveCount(1);
            var identifier = got.First();
            identifier.Kind.Should().Be("Deployment");
            identifier.Name.Should().Be("nginx");
            identifier.Namespace.Should().Be("default");
        }

        [Test]
        public void ShouldHandleMultipleResourcesDefinedInTheSameFile()
        {
            var input = ResourceLoader.Load("multiple-resources.yaml");
            var got = KubernetesYaml.GetDefinedResources(input);
            got.Should().HaveCount(3);
        }
    }
}