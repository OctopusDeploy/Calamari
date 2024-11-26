using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class ReplicaSetTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""apiVersion"": ""apps/v1"",
    ""kind"": ""ReplicaSet"",
    ""metadata"": {
        ""name"": ""nginx"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""status"": {
        ""availableReplicas"": 2,
        ""readyReplicas"": 2,
        ""replicas"": 3,
    }
}";
            var replicaSet = ResourceFactory.FromJson(input, new Options());
            
            replicaSet.Should().BeEquivalentTo(new
            {
                Group = "apps",
                Version = "v1",
                Kind = "ReplicaSet",
                Name = "nginx",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                Desired = 3,
                Ready = 2,
                Current = 2,
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress
            });
        }
    }
}