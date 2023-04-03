using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class IngressTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""kind"": ""Ingress"",
    ""metadata"": {
        ""name"": ""my-ingress"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""spec"": {
        ""ingressClassName"": ""nginx"",
        ""rules"": [
            {
                ""host"": ""host"",
                ""http"": {
                    ""paths"": [
                        {
                            ""path"": ""/"",
                            ""pathType"": ""Exact"",
                            ""backend"": {
                                ""name"": ""my-svc"",
                                ""port"": {
                                    ""number"": 80
                                }
                            }
                        }
                    ]
                }
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
            var ingress = ResourceFactory.FromJson(input);
            
            ingress.Should().BeEquivalentTo(new
            {
                Kind = "Ingress",
                Name = "my-ingress",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                Hosts = new string[] 
                {
                    "host"
                },
                Address = "192.168.49.2",
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful
            });
        }
    }
}