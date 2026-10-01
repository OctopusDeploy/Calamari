using Calamari.Kubernetes.ResourceStatus;
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
    ""apiVersion"": ""apps/v1"",
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
            var daemonSet = ResourceFactory.FromJson(input, new Options());
            
            daemonSet.Should().BeEquivalentTo(new
            {
                GroupVersionKind = SupportedResourceGroupVersionKinds.DaemonSetV1,
                Name = "my-ds",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                Desired = 2,
                Current = 1,
                Ready = 1,
                UpToDate = 1,
                Available = 1,
                NodeSelector = "arch=amd64,os=linux",
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress
            });
        }

        [Test]
        public void ShouldBeSuccessfulWhenUpdatedAndAvailableMatchDesired()
        {
            var daemonSet = ResourceFactory.FromJson(DaemonSet(desired: 2, updated: 2, available: 2, ready: 2), new Options());

            daemonSet.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful);
        }

        [Test]
        public void ShouldBeInProgressWhenGenerationIsGreaterThanObservedGeneration()
        {
            var daemonSet = ResourceFactory.FromJson(DaemonSet(desired: 2, updated: 2, available: 2, ready: 2, generation: 2, observedGeneration: 1), new Options());

            daemonSet.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress);
        }

        [Test]
        public void ShouldNotRequireReadyToMatchDesired()
        {
            // gitops-engine considers the rollout finished once updated and available pods match desired; it does not wait on numberReady.
            var daemonSet = ResourceFactory.FromJson(DaemonSet(desired: 2, updated: 2, available: 2, ready: 0), new Options());

            daemonSet.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful);
        }

        [Test]
        public void WhenUsingLegacyChecks_RequiresReadyToMatchDesired()
        {
            var daemonSet = ResourceFactory.FromJson(DaemonSet(desired: 2, updated: 2, available: 2, ready: 0),
                new Options { EnableLegacyResourceStatusChecks = true });

            daemonSet.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress);
        }

        static string DaemonSet(int desired, int updated, int available, int ready, int generation = 1, int observedGeneration = 1)
        {
            return $@"{{
    ""apiVersion"": ""apps/v1"",
    ""kind"": ""DaemonSet"",
    ""metadata"": {{
        ""name"": ""my-ds"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62"",
        ""generation"": {generation}
    }},
    ""status"": {{
        ""desiredNumberScheduled"": {desired},
        ""currentNumberScheduled"": {updated},
        ""numberAvailable"": {available},
        ""numberReady"": {ready},
        ""updatedNumberScheduled"": {updated},
        ""observedGeneration"": {observedGeneration}
    }}
}}";
        }
    }
}

