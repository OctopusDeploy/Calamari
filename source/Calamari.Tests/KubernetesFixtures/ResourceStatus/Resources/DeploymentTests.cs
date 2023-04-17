using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class DeploymentTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""kind"": ""Deployment"",
    ""metadata"": {
        ""name"": ""nginx"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""spec"": {
        ""replicas"": 3
    },
    ""status"": {
        ""availableReplicas"": 3,
        ""readyReplicas"": 3,
        ""updatedReplicas"": 1
    }
}";
            var deployment = ResourceFactory.FromJson(input);
            
            deployment.Should().BeEquivalentTo(new
            {
                Kind = "Deployment",
                Name = "nginx",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                UpToDate = 1,
                Ready = "3/3",
                Available = 3,
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress
            });
        }
    }
}