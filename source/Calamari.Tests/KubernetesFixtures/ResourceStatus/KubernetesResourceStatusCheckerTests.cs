using System.IO;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Microsoft.Azure.Management.BatchAI.Fluent.Models;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus;

[TestFixture]
public class KubernetesResourcesTests
{
    [Test]
    public void ReturnsCorrectObjectHierarchyForDeployments()
    {
        var kubectl = new MockKubectlCommand(System.IO.File.ReadAllText("KubernetesFixtures/ResourceStatus/deployment-with-3-replicas.json"));
        var statusChecker = new KubernetesResourceStatusChecker(kubectl);

        var got = statusChecker.GetHierarchyStatuses(new KubernetesResource { Kind = "Deployment", Name = "nginx" }, null);

        got.Should().HaveCount(5);
    }

}

public class MockKubectlCommand : IKubectlCommand
{
    private IEnumerable<JObject> data;

    public MockKubectlCommand(string json)
    {
        data = JArray.Parse(json).Cast<JObject>();
    }

    public string Get(string kind, string name, ICommandLineRunner commandLineRunner)
    {
        return data
            .FirstOrDefault(item => item.SelectToken("$.kind").Value<string>() == kind &&
                                    item.SelectToken("$.metadata.name").Value<string>() == name)
            ?.ToString();
    }

    public string GetAll(string kind, ICommandLineRunner commandLineRunner)
    {
        var items = new JArray(data.Where(item => item.SelectToken("$.kind").Value<string>() == kind));
        return $"{{items: {items}}}";
    }
}