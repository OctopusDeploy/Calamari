using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class StatefulSetTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            const string input = @"{
    ""apiVersion"": ""apps/v1"",
    ""kind"": ""StatefulSet"",
    ""metadata"": {
        ""name"": ""my-sts"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    },
    ""status"": {
        ""readyReplicas"": 2,
        ""replicas"": 3
    }
}";
            var statefulSet = ResourceFactory.FromJson(input, new Options());

            statefulSet.Should().BeEquivalentTo(new
            {
                GroupVersionKind = SupportedResourceGroupVersionKinds.StatefulSetV1,
                Name = "my-sts",
                Namespace = "default",
                Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                Ready = "2/3",
                ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress
            });
        }

        [Test]
        public void ShouldBeSuccessfulWhenObservedAndAllReplicasReadyAtCurrentRevision()
        {
            var statefulSet = ResourceFactory.FromJson(StatefulSet(specReplicas: 3, readyReplicas: 3), new Options());

            statefulSet.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful);
        }

        [Test]
        public void ShouldBeInProgressWhenReadyReplicasLessThanSpec()
        {
            var statefulSet = ResourceFactory.FromJson(StatefulSet(specReplicas: 3, readyReplicas: 2), new Options());

            statefulSet.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress);
        }

        [Test]
        public void ShouldBeInProgressWhenGenerationIsGreaterThanObservedGeneration()
        {
            var statefulSet = ResourceFactory.FromJson(StatefulSet(specReplicas: 3, readyReplicas: 3, generation: 2, observedGeneration: 1), new Options());

            statefulSet.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress);
        }

        [Test]
        public void ShouldBeInProgressWhenUpdateRevisionDiffersFromCurrentRevision()
        {
            var statefulSet = ResourceFactory.FromJson(StatefulSet(specReplicas: 3, readyReplicas: 3, updateRevision: "rev2", currentRevision: "rev1"), new Options());

            statefulSet.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress);
        }

        [Test]
        public void WhenUsingLegacyChecks_StatusComparesReadyToStatusReplicas()
        {
            var statefulSet = ResourceFactory.FromJson(StatefulSet(readyReplicas: 3, statusReplicas: 3, observedGeneration: 0),
                new Options { EnableLegacyResourceStatusChecks = true });

            statefulSet.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful);
        }

        static string StatefulSet(
            int? specReplicas = null,
            int readyReplicas = 0,
            int? statusReplicas = null,
            int generation = 1,
            int observedGeneration = 1,
            string updateRevision = "rev1",
            string currentRevision = "rev1")
        {
            var spec = specReplicas.HasValue ? $@"""spec"": {{ ""replicas"": {specReplicas.Value} }}," : "";
            return $@"{{
    ""apiVersion"": ""apps/v1"",
    ""kind"": ""StatefulSet"",
    ""metadata"": {{
        ""name"": ""my-sts"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62"",
        ""generation"": {generation}
    }},
    {spec}
    ""status"": {{
        ""readyReplicas"": {readyReplicas},
        ""replicas"": {statusReplicas ?? readyReplicas},
        ""updatedReplicas"": {readyReplicas},
        ""observedGeneration"": {observedGeneration},
        ""updateRevision"": ""{updateRevision}"",
        ""currentRevision"": ""{currentRevision}""
    }}
}}";
        }
    }
}
