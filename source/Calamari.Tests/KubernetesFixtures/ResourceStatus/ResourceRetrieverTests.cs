using System.Collections.Generic;
using System.Linq;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    [TestFixture]
    public class ResourceRetrieverTests
    {
        [Test]
        public void ReturnsCorrectObjectHierarchyForDeployments()
        {
            var kubectlGet = new MockKubectlGet(TestFileLoader.Load("deployment-with-3-replicas.json"));
            var resourceRetriever = new ResourceRetriever(kubectlGet);

            var got = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier("Deployment", "nginx", "octopus")
                },
                null);
            
            got.Should().BeEquivalentTo(new object[]
            {
                new
                {
                    Kind = "Deployment",
                    Name = "nginx",
                    Namespace = "octopus",
                    Children = new object[]
                    {
                        new
                        {
                            Kind = "ReplicaSet",
                            Children = new object[]
                            {
                                new {Kind = "Pod"},
                                new {Kind = "Pod"},
                                new {Kind = "Pod"}
                            }
                        }
                    }
                },
                new
                {
                    Kind = "ReplicaSet",
                    Children = new object[]
                    {
                        new {Kind = "Pod"},
                        new {Kind = "Pod"},
                        new {Kind = "Pod"}
                    }
                },
                new {Kind = "Pod"},
                new {Kind = "Pod"},
                new {Kind = "Pod"}
            });
        }
    
        [Test]
        public void ReturnsCorrectObjectHierarchyForMultipleResources()
        {
            var kubectlGet = new MockKubectlGet(TestFileLoader.Load("2-deployments-with-3-replicas-each.json"));
            var resourceRetriever = new ResourceRetriever(kubectlGet);
    
            var got = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier("Deployment", "nginx", "default"),
                    new ResourceIdentifier("Deployment", "curl" , "default"),
                },
                null);

            got.Should().BeEquivalentTo(new object[]
            {
                new
                {
                    Kind = "Deployment",
                    Name = "nginx",
                },
                new
                {
                    Kind = "ReplicaSet",
                },
                new {Kind = "Pod"},
                new {Kind = "Pod"},
                new {Kind = "Pod"},
                new
                {
                    Kind = "Deployment",
                    Name = "curl",
                },
                new
                {
                    Kind = "ReplicaSet",
                },
                new {Kind = "Pod"},
                new {Kind = "Pod"},
                new {Kind = "Pod"}
            });
        }
    }

    public class MockKubectlGet : IKubectlGet
    {
        private readonly IEnumerable<JObject> data;

        public MockKubectlGet(string json)
        {
            data = JArray.Parse(json).Cast<JObject>();
        }
        
        public string Resource(string kind, string name, string @namespace, Kubectl kubectl)
        {
            var result = data
                .FirstOrDefault(item =>
                    item.SelectToken("$.kind")!.Value<string>() == kind
                    && item.SelectToken("$.metadata.name")!.Value<string>() == name
                    && item.SelectToken($".metadata.namespace")!.Value<string>() == @namespace);
            return result == null ? string.Empty : result.ToString();
        }

        public string AllResources(string kind, string @namespace, Kubectl kubectl)
        {
            var items = new JArray(data.Where(item =>
                item.SelectToken("$.kind")!.Value<string>() == kind &&
                item.SelectToken($".metadata.namespace")!.Value<string>() == @namespace));
            return $"{{items: {items}}}";
        }
    }
}