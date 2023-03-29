using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class DaemonSetTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""kind"": ""DaemonSet"",
    ""metadata"": {
        ""name"": ""my-ds"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""spec"": {
        ""template"": {
            ""spec"": {
                ""nodeSelector"": {
                    ""os"": ""linux"",
                    ""arch"": ""amd64""
                }
            }
        }
    },
    ""status"": {
        ""currentNumberScheduled"": 1,
        ""desiredNumberScheduled"": 2,
        ""numberAvailable"": 1,
        ""numberReady"": 1,
        ""updatedNumberScheduled"": 1
    }
}";
            var daemonSet = ResourceFactory.FromJson(input);
            
            daemonSet.Should().BeEquivalentTo(new
            {
                Kind = "DaemonSet",
                Name = "my-ds",
                Namespace = "default",
                Desired = 2,
                Current = 1,
                Ready = 1,
                UpToDate = 1,
                Available = 1,
                NodeSelector = "arch=amd64,os=linux",
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress
            });
        }
    }
}

