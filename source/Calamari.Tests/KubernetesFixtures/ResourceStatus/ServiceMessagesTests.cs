using System.IO;
using System.Linq;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    [TestFixture]
    public class ServiceMessagesTests
    {
        [Test]
        public void ShouldReturnTheCorrectDataShape()
        {
            // var input = File.ReadAllText("KubernetesFixtures/ResourceStatus/deployment-with-3-replicas.json");
            // var resources = JArray.Parse(input).Select(item => ResourceFactory.FromJObject((JObject)item));
            // var got = ServiceMessages.GenerateServiceMessageData(resources);
            // var parsed = JObject.Parse(got);
            // parsed.SelectTokens("$.Pod[*]").Should().HaveCount(3);
            // parsed.SelectTokens("$.Deployment[*]").Should().HaveCount(1);
            // parsed.SelectTokens("$.ReplicaSet[*]").Should().HaveCount(1);
        }
    }
}