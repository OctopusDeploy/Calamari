using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class PersistentVolumeTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""apiVersion"": ""v1"",
    ""kind"": ""PersistentVolume"",
    ""metadata"": {
        ""name"": ""my-pv"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""spec"": {
        ""capacity"": {
            ""storage"": ""1Mi""
        },
        ""persistentVolumeReclaimPolicy"": ""Retain""
    },
    ""status"": {
        ""phase"": ""Bound""
    }
}";
            var persistentVolume = ResourceFactory.FromJson(input, new Options());
            
            persistentVolume.Should().BeEquivalentTo(new
            {
                GroupVersionKind = SupportedResourceGroupVersionKinds.PersistentVolumeV1,
                Name = "my-pv",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                Status = "Bound",
                Capacity = "1Mi",
                ReclaimPolicy = "Retain",
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful
            });
        }
    }
}

