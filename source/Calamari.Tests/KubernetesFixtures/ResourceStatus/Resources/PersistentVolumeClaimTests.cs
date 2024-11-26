using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class PersistentVolumeClaimTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""apiVersion"": ""v1"",
    ""kind"": ""PersistentVolumeClaim"",
    ""metadata"": {
        ""name"": ""my-pvc"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""spec"": {
        ""volumeName"": ""pvc-08cdb1f6-42e4-4938-b4c7-0576030a8da6"",
        ""storageClassName"": ""standard""
    },
    ""status"": {
        ""phase"": ""Bound"",
        ""accessModes"": [
            ""ReadWriteOnce""
        ],
        ""capacity"": {
            ""storage"": ""1Mi""
        }
    }
}";
            var persistentVolumeClaim = ResourceFactory.FromJson(input, new Options());
            
            persistentVolumeClaim.Should().BeEquivalentTo(new
            {
                Group = "",
                Version = "v1",
                Kind = "PersistentVolumeClaim",
                Name = "my-pvc",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                Status = "Bound",
                Volume = "pvc-08cdb1f6-42e4-4938-b4c7-0576030a8da6",
                Capacity = "1Mi",
                AccessModes = new string []
                {
                    "ReadWriteOnce"
                },
                StorageClass = "standard",
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful
            });
        }
    }
}

