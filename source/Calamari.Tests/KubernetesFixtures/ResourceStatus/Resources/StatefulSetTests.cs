using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class StatefulSetTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""apiVersion"": ""apps/v1"",
    ""kind"": ""StatefulSet"",
    ""metadata"": {
        ""name"": ""my-sts"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""status"": {
        ""readyReplicas"": 2,
        ""replicas"": 3
    }
}";
            var statefulSet = ResourceFactory.FromJson(input, new Options());
            
            statefulSet.Should().BeEquivalentTo(new
            {
                GroupVersionKind = SupportedResourceGroupVersionKinds.StatefulSetV1,
                Name = "my-sts",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                Ready = "2/3",
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress
            });
        }
    }
}

