using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Kubernetes.ResourceStatus;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    //[TestFixture]
    public class ResourceRetrieverTests
    {
        //[Test]
        public void ReturnsCorrectObjectHierarchyForDeployments()
        {
            var kubectl = new MockKubectl(File.ReadAllText("KubernetesFixtures/ResourceStatus/deployment-with-3-replicas.json"));
            var resourceRetriever = new ResourceRetriever(kubectl);
    
            var got = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier() { Kind = "Deployment", Name = "nginx" }
                }, "local", "Action-1", null);
    
            got.Should().HaveCount(5);
        }
    
        //[Test]
        public void ReturnsCorrectObjectHierarchyForMultipleResources()
        {
            var kubectl = new MockKubectl(File.ReadAllText("KubernetesFixtures/ResourceStatus/2-deployments-with-3-replicas-each.json"));
            var resourceRetriever = new ResourceRetriever(kubectl);
    
            var got = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier() { Kind = "Deployment", Name = "nginx" },
                    new ResourceIdentifier() { Kind = "Deployment", Name = "curl" }
                }, "local", "Action-1", null);
    
            got.Should().HaveCount(10);
        }
    }
    
    public class MockKubectl : IKubectl
    {
        private readonly IEnumerable<JObject> data;
    
        public MockKubectl(string json)
        {
            data = JArray.Parse(json).Cast<JObject>();
        }
    
        public string Get(string kind, string name, string @namespace, ICommandLineRunner commandLineRunner)
        {
            return data
                .FirstOrDefault(item => item.SelectToken("$.kind").Value<string>() == kind &&
                                        item.SelectToken("$.metadata.name").Value<string>() == name)
                ?.ToString();
        }
    
        public string GetAll(string kind, string @namespace, ICommandLineRunner commandLineRunner)
        {
            var items = new JArray(data.Where(item => item.SelectToken("$.kind").Value<string>() == kind));
            return $"{{items: {items}}}";
        }
    }
}