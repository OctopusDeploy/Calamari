using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class ServiceTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""kind"": ""Service"",
    ""metadata"": {
        ""name"": ""my-svc"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""spec"": {
        ""type"": ""LoadBalancer"",
        ""clusterIP"": ""10.96.0.1"",
        ""ports"": [
            {
                ""name"": ""https"",
                ""port"": 443,
                ""protocol"": ""TCP"",
                ""targetPort"": 8443
            },
            {
                ""name"": """",
                ""port"": 80,
                ""protocol"": ""TCP"",
                ""targetPort"": 8080,
                ""nodePort"": 30080
            }
        ]
    },
    ""status"": {
        ""loadBalancer"": {
            ""ingress"": [
                {
                    ""ip"": ""192.168.49.2""
                }
            ]

        }
    }
}";
            var service = ResourceFactory.FromJson(input, new Options());
            
            service.Should().BeEquivalentTo(new
            {
                Kind = "Service",
                Name = "my-svc",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                Type = "LoadBalancer",
                ClusterIp = "10.96.0.1",
                ExternalIp = "192.168.49.2",
                Ports = new string[] 
                {
                    "443/TCP",
                    "80:30080/TCP"
                },
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful
            });
        }
    }
}
