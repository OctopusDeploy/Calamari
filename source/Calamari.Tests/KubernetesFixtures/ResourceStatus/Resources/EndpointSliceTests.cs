using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class EndpointSliceTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""apiVersion"": ""discovery.k8s.io/v1"",
    ""kind"": ""EndpointSlice"",
    ""metadata"": {
        ""name"": ""my-svc-abcde"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""addressType"": ""IPv4"",
    ""ports"": [ 
        {
            ""name"": """",
            ""port"": 8080,
            ""protocol"": ""TCP""
        }
    ],
    ""endpoints"": [
        {
            ""addresses"": [
                ""10.244.19.164""
            ],
        },
        {
            ""addresses"": [
                ""10.244.19.165""
            ],
        },
        {
            ""addresses"": [
                ""10.244.19.166"",
                ""10.244.19.167""
            ],
        }
    ]
}";
            var service = ResourceFactory.FromJson(input, new Options());
            
            service.Should().BeEquivalentTo(new
            {
                Group = "discovery.k8s.io",
                Version = "v1",
                Kind = "EndpointSlice",
                Name = "my-svc-abcde",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                AddressType = "IPv4",
                Ports = new string[] 
                {
                    "8080",
                },
                Endpoints = new string[]
                {
                    "10.244.19.164", "10.244.19.165", "10.244.19.166", "10.244.19.167"
                },
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful
            });
        }
    }
}

