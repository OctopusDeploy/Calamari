using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class CronJobTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""kind"": ""CronJob"",
    ""metadata"": {
        ""name"": ""my-cj"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""spec"": {
        ""schedule"": ""* * * * *"",
        ""suspend"": false
    }
}";
            var cronJob = ResourceFactory.FromJson(input);
            
            cronJob.Should().BeEquivalentTo(new
            {
                Kind = "CronJob",
                Name = "my-cj",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                Schedule = "* * * * *",
                Suspend = false,
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful
            });
        }
    }
}

