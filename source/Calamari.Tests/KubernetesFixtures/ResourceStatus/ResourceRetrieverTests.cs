using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Octopus.CoreUtilities;
using Octopus.Versioning.Semver;

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
    
            got.Should().HaveCount(5);
            got.Where(resource => resource.Kind == "Deployment").Should().HaveCount(1);
            got.Where(resource => resource.Kind == "ReplicaSet").Should().HaveCount(1);
            got.Where(resource => resource.Kind == "Pod").Should().HaveCount(3);
            got.First(resource => resource.Kind == "Deployment").Children.Should().HaveCount(1);
            got.First(resource => resource.Kind == "ReplicaSet").Children.Should().HaveCount(3);
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
    
            got.Should().HaveCount(10);
            got.Where(resource => resource.Kind == "Deployment").Should().HaveCount(2);
            got.Where(resource => resource.Kind == "ReplicaSet").Should().HaveCount(2);
            got.Where(resource => resource.Kind == "Pod").Should().HaveCount(6);
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
                    item.SelectToken("$.kind").Value<string>() == kind
                    && item.SelectToken("$.metadata.name").Value<string>() == name
                    && item.SelectToken($".metadata.namespace").Value<string>() == @namespace);
            return result == null ? string.Empty : result.ToString();
        }

        public string AllResources(string kind, string @namespace, Kubectl kubectl)
        {
            var items = new JArray(data.Where(item =>
                item.SelectToken("$.kind").Value<string>() == kind &&
                item.SelectToken($".metadata.namespace").Value<string>() == @namespace));
            return $"{{items: {items}}}";
        }
    }
}