using System;
using System.Linq;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class DeploymentTests
    {
        [Test]
        public void ShouldCollectCorrectProperties()
        {
            var input = new DeploymentResponseBuilder()
                        .WithDesiredReplicas(3)
                        .WithTotalReplicas(4)
                        .WithAvailableReplicas(3)
                        .WithReadyReplicas(3)
                        .WithUpdatedReplicas(1)
                        .Build();
            var deployment = ResourceFactory.FromJson(input, new Options());

            deployment.Should()
                      .BeEquivalentTo(new
                      {
                          GroupVersionKind = SupportedResourceGroupVersionKinds.DeploymentV1,
                          Name = "nginx",
                          Namespace = "default",
                          Uid = "01695a39-5865-4eea-b4bf-1a4783cbce62",
                          UpToDate = 1,
                          Ready = "3/3",
                          Available = 3,
                          ResourceStatus = Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress
                      });
        }

        [Test]
        public void ShouldNotBeSuccessfulIfSomeChildrenPodsAreStillRunning()
        {
            var input = new DeploymentResponseBuilder()
                        .WithDesiredReplicas(3)
                        .WithTotalReplicas(3)
                        .WithAvailableReplicas(3)
                        .WithReadyReplicas(3)
                        .WithUpdatedReplicas(3)
                        .Build();
            var deployment = ResourceFactory.FromJson(input, new Options());

            var pod = new PodResponseBuilder().Build();
            // More pods remaining than desired
            var children = Enumerable.Range(0, 4)
                                     .Select(_ => ResourceFactory.FromJson(pod, new Options()));

            var replicaSet = new Resource();
            replicaSet.UpdateChildren(children);

            deployment.UpdateChildren(new[] { replicaSet });

            deployment.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress);
        }

        [Test]
        public void ShouldBeSuccessfulIfOnlyDesiredPodsAreRunning()
        {
            var input = new DeploymentResponseBuilder()
                        .WithDesiredReplicas(3)
                        .WithTotalReplicas(3)
                        .WithAvailableReplicas(3)
                        .WithReadyReplicas(3)
                        .WithUpdatedReplicas(3)
                        .Build();
            var deployment = ResourceFactory.FromJson(input, new Options());

            var pod = new PodResponseBuilder().Build();
            var children = Enumerable.Range(0, 3)
                                     .Select(_ => ResourceFactory.FromJson(pod, new Options()));

            var replicaSet = new Resource();
            replicaSet.UpdateChildren(children);

            deployment.UpdateChildren(new[] { replicaSet });

            deployment.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful);
        }

        [Test]
        public void ShouldBeFailedWhenProgressDeadlineExceeded()
        {
            var input = new DeploymentResponseBuilder()
                        .WithDesiredReplicas(3)
                        .WithTotalReplicas(1)
                        .WithAvailableReplicas(1)
                        .WithReadyReplicas(1)
                        .WithUpdatedReplicas(1)
                        .WithGeneration(2)
                        .WithObservedGeneration(2)
                        .WithProgressDeadlineExceeded()
                        .Build();
            var deployment = ResourceFactory.FromJson(input, new Options());

            deployment.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.Failed);
        }

        [Test]
        public void ShouldBeInProgressWhenGenerationIsGreaterThanObservedGeneration()
        {
            var input = new DeploymentResponseBuilder()
                        .WithDesiredReplicas(3)
                        .WithTotalReplicas(3)
                        .WithAvailableReplicas(3)
                        .WithReadyReplicas(3)
                        .WithUpdatedReplicas(3)
                        .WithGeneration(3)
                        .WithObservedGeneration(2)
                        .Build();
            var deployment = ResourceFactory.FromJson(input, new Options());

            deployment.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress);
        }

        [Test]
        public void ShouldBeInProgressWhenUpToDateIsLessThanDesired()
        {
            var input = new DeploymentResponseBuilder()
                        .WithDesiredReplicas(3)
                        .WithTotalReplicas(3)
                        .WithAvailableReplicas(3)
                        .WithReadyReplicas(3)
                        .WithUpdatedReplicas(2)
                        .WithGeneration(1)
                        .WithObservedGeneration(1)
                        .Build();
            var deployment = ResourceFactory.FromJson(input, new Options());

            deployment.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress);
        }

        [Test]
        public void ShouldBeInProgressWhenTotalReplicasIsGreaterThanUpToDate()
        {
            var input = new DeploymentResponseBuilder()
                        .WithDesiredReplicas(3)
                        .WithTotalReplicas(4)
                        .WithAvailableReplicas(3)
                        .WithReadyReplicas(3)
                        .WithUpdatedReplicas(3)
                        .WithGeneration(1)
                        .WithObservedGeneration(1)
                        .Build();
            var deployment = ResourceFactory.FromJson(input, new Options());

            deployment.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress);
        }

        [Test]
        public void ShouldBeInProgressWhenAvailableIsLessThanUpToDate()
        {
            var input = new DeploymentResponseBuilder()
                        .WithDesiredReplicas(3)
                        .WithTotalReplicas(3)
                        .WithAvailableReplicas(2)
                        .WithReadyReplicas(3)
                        .WithUpdatedReplicas(3)
                        .WithGeneration(1)
                        .WithObservedGeneration(1)
                        .Build();
            var deployment = ResourceFactory.FromJson(input, new Options());

            deployment.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress);
        }
    }

    class DeploymentResponseBuilder
    {
        const string Template = @"{{
            ""apiVersion"": ""apps/v1"",
            ""kind"": ""Deployment"",
            ""metadata"": {{
                ""name"": ""nginx"",
                ""namespace"": ""default"",
                ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62"",
                ""generation"": {5}
            }},
            ""spec"": {{
                ""replicas"": {0}
            }},
            ""status"": {{
                ""replicas"": {1},
                ""availableReplicas"": {2},
                ""readyReplicas"": {3},
                ""updatedReplicas"": {4},
                ""observedGeneration"": {6}{7}
            }}
        }}";

        int DesiredReplicas { get; set; }
        int TotalReplicas { get; set; }
        int AvailableReplicas { get; set; }
        int ReadyReplicas { get; set; }
        int UpdatedReplicas { get; set; }
        int Generation { get; set; } = 1;
        int ObservedGeneration { get; set; } = 1;
        string Conditions { get; set; } = "";

        public DeploymentResponseBuilder WithDesiredReplicas(int replicas)
        {
            DesiredReplicas = replicas;
            return this;
        }

        public DeploymentResponseBuilder WithTotalReplicas(int replicas)
        {
            TotalReplicas = replicas;
            return this;
        }

        public DeploymentResponseBuilder WithAvailableReplicas(int replicas)
        {
            AvailableReplicas = replicas;
            return this;
        }

        public DeploymentResponseBuilder WithReadyReplicas(int replicas)
        {
            ReadyReplicas = replicas;
            return this;
        }

        public DeploymentResponseBuilder WithUpdatedReplicas(int replicas)
        {
            UpdatedReplicas = replicas;
            return this;
        }

        public DeploymentResponseBuilder WithGeneration(int generation)
        {
            Generation = generation;
            return this;
        }

        public DeploymentResponseBuilder WithObservedGeneration(int observedGeneration)
        {
            ObservedGeneration = observedGeneration;
            return this;
        }

        public DeploymentResponseBuilder WithProgressDeadlineExceeded()
        {
            Conditions = @",
        ""conditions"": [
            {
                ""type"": ""Progressing"",
                ""status"": ""False"",
                ""reason"": ""ProgressDeadlineExceeded"",
                ""message"": ""ReplicaSet has timed out progressing.""
            }
        ]";
            return this;
        }

        public string Build()
        {
            return string.Format(
                                 Template,
                                 DesiredReplicas,
                                 TotalReplicas,
                                 AvailableReplicas,
                                 ReadyReplicas,
                                 UpdatedReplicas,
                                 Generation,
                                 ObservedGeneration,
                                 Conditions);
        }
    }
}