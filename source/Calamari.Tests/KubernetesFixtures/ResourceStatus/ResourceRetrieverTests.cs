using System.IO;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Kubernetes;
using Calamari.ResourceStatus;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Microsoft.Azure.Management.BatchAI.Fluent.Models;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus;

[TestFixture]
public class ResourceRetrieverTests
{
    [Test]
    public void ReturnsCorrectObjectHierarchyForDeployments()
    {
        var kubectl = new MockKubectl(System.IO.File.ReadAllText("KubernetesFixtures/ResourceStatus/deployment-with-3-replicas.json"));
        var resourceRetriever = new ResourceRetriever(kubectl);

        var got = resourceRetriever.GetAllOwnedResources(new ResourceIdentifier { Kind = "Deployment", Name = "nginx" }, null);

        got.Should().HaveCount(5);
    }

}

public class MockKubectl : IKubectl
{
    private IEnumerable<JObject> data;

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