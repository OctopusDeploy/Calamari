using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class JobTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""kind"": ""Job"",
    ""metadata"": {
        ""name"": ""my-job"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""spec"": {
        ""backoffLimit"": 4,
        ""completions"": 3
    },
    ""status"": {
        ""succeeded"": 3,
        ""startTime"": ""2023-03-29T00:00:00Z"",
        ""completionTime"": ""2023-03-30T02:03:04Z""
    }
}";
            var configMap = ResourceFactory.FromJson(input);
            
            configMap.Should().BeEquivalentTo(new
            {
                Kind = "Job",
                Name = "my-job",
                Namespace = "default",
                Completions = "3/3",
                Duration = "1.02:03:04",
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful
            });
        }
    }
}

