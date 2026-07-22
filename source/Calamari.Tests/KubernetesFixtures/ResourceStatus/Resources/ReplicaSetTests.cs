using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class ReplicaSetTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""apiVersion"": ""apps/v1"",
    ""kind"": ""ReplicaSet"",
    ""metadata"": {
        ""name"": ""nginx"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""spec"": {
        ""replicas"": 3
    },
    ""status"": {
        ""availableReplicas"": 2,
        ""readyReplicas"": 2,
        ""replicas"": 3,
    }
}";
            var replicaSet = ResourceFactory.FromJson(input, new Options());

            replicaSet.Should().BeEquivalentTo(new
            {
                GroupVersionKind = SupportedResourceGroupVersionKinds.ReplicaSetV1,
                Name = "nginx",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                Desired = 3,
                Ready = 2,
                Current = 2,
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress
            });
        }

        [Test]
        public void ShouldBeSuccessfulWhenAvailableReplicasMatchesSpec()
        {
            var replicaSet = ResourceFactory.FromJson(ReplicaSet(specReplicas: 3, availableReplicas: 3), new Options());

            replicaSet.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful);
        }

        [Test]
        public void ShouldBeInProgressWhenGenerationIsGreaterThanObservedGeneration()
        {
            var replicaSet = ResourceFactory.FromJson(ReplicaSet(specReplicas: 3, availableReplicas: 3, generation: 2, observedGeneration: 1), new Options());

            replicaSet.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress);
        }

        [Test]
        public void ShouldBeFailedWhenReplicaFailureConditionIsTrue()
        {
            const string conditions = @",""conditions"": [ { ""type"": ""ReplicaFailure"", ""status"": ""True"" } ]";
            var replicaSet = ResourceFactory.FromJson(ReplicaSet(specReplicas: 3, availableReplicas: 0, conditions: conditions), new Options());

            replicaSet.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.Failed);
        }

        [Test]
        public void WhenUsingLegacyChecks_StatusIsBasedOnStatusReplicas()
        {
            // No spec.replicas: the legacy check compares status.replicas, readyReplicas and availableReplicas.
            var replicaSet = ResourceFactory.FromJson(ReplicaSet(availableReplicas: 3, readyReplicas: 3, statusReplicas: 3),
                new Options { EnableLegacyResourceStatusChecks = true });

            replicaSet.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful);
        }

        static string ReplicaSet(
            int? specReplicas = null,
            int availableReplicas = 0,
            int readyReplicas = 0,
            int statusReplicas = 0,
            int generation = 0,
            int observedGeneration = 0,
            string conditions = "")
        {
            var spec = specReplicas.HasValue ? $@"""spec"": {{ ""replicas"": {specReplicas.Value} }}," : "";
            return $@"{{
    ""apiVersion"": ""apps/v1"",
    ""kind"": ""ReplicaSet"",
    ""metadata"": {{
        ""name"": ""nginx"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62"",
        ""generation"": {generation}
    }},
    {spec}
    ""status"": {{
        ""availableReplicas"": {availableReplicas},
        ""readyReplicas"": {readyReplicas},
        ""replicas"": {statusReplicas},
        ""observedGeneration"": {observedGeneration}{conditions}
    }}
}}";
        }
    }
}
