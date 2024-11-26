using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class ConfigMapTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""apiVersion"": ""v1"",
    ""kind"": ""ConfigMap"",
    ""metadata"": {
        ""name"": ""my-cm"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""data"": {
        ""x"": ""y"",
        ""a"": ""b""
    }
}";
            var configMap = ResourceFactory.FromJson(input, new Options());
            
            configMap.Should().BeEquivalentTo(new
            {
                Group = "",
                Version = "v1",
                Kind = "ConfigMap",
                Name = "my-cm",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                Data = 2,
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful
            });
        }
    }
}

