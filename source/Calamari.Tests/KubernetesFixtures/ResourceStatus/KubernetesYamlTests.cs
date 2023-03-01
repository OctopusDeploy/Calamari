using System.IO;
using Calamari.Kubernetes.ResourceStatus;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    //[TestFixture]
    public class KubernetesYamlTests
    {
        //[Test]
        public void ShouldGenerateCorrectIdentifiers()
        {
            var input = File.ReadAllText("KubernetesFixtures/ResourceStatus/multiple-resources.yaml");
            var got = KubernetesYaml.GetDefinedResources(input);
            got.Should().HaveCount(3);
        }
    }
}