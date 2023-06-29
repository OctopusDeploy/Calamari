using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class SecretTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""kind"": ""Secret"",
    ""metadata"": {
        ""name"": ""my-secret"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""type"": ""Opaque"",
    ""data"": {
        ""x"": ""y"",
        ""a"": ""b""
    }
}";
            var secret = ResourceFactory.FromJson(input, new Options());
            
            secret.Should().BeEquivalentTo(new
            {
                Kind = "Secret",
                Name = "my-secret",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                Data = 2,
                Type = "Opaque",
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful
            });
        }
    }
}

