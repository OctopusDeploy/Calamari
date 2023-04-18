using System.Linq;
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
            var deployment = ResourceFactory.FromJson(input);
            
            deployment.Should().BeEquivalentTo(new
            {
                Kind = "Deployment",
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
            var deployment = ResourceFactory.FromJson(input);
            
            var pod = new PodResponseBuilder().Build();
            // More pods remaining than desired
            var children = Enumerable.Range(0, 4) 
                .Select(_ => ResourceFactory.FromJson(pod));
            
            var replicaSet = ResourceFactory.FromJson("{}");
            replicaSet.UpdateChildren(children);
            
            deployment.UpdateChildren(new Resource[] { replicaSet });

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
            var deployment = ResourceFactory.FromJson(input);
            
            var pod = new PodResponseBuilder().Build();
            var children = Enumerable.Range(0, 3)
                .Select(_ => ResourceFactory.FromJson(pod));
            
            var replicaSet = ResourceFactory.FromJson("{}");
            replicaSet.UpdateChildren(children);
            
            deployment.UpdateChildren(new Resource[] { replicaSet });

            deployment.ResourceStatus.Should().Be(Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful);
        }
    }

    internal class DeploymentResponseBuilder
    {
        private const string Template = @"{{
    ""kind"": ""Deployment"",
    ""metadata"": {{
        ""name"": ""nginx"",
        ""namespace"": ""default"",
        ""uid"": ""01695a39-5865-4eea-b4bf-1a4783cbce62""
    }},
    ""spec"": {{
        ""replicas"": {0}
    }},
    ""status"": {{
        ""replicas"": {1},
        ""availableReplicas"": {2},
        ""readyReplicas"": {3},
        ""updatedReplicas"": {4}
    }}
}}";

        private int DesiredReplicas { get; set; }
        private int TotalReplicas { get; set; }
        private int AvailableReplicas { get; set; }
        private int ReadyReplicas { get; set; }
        private int UpdatedReplicas { get; set; }

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
        
        public string Build()
        {
            return string.Format(
                Template, 
                DesiredReplicas, 
                TotalReplicas, 
                AvailableReplicas, 
                ReadyReplicas,
                UpdatedReplicas);
        }
    }
}