using System.Collections.Generic;
using System.Linq;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    [TestFixture]
    public class ResourceStatusCheckerTests
    {
        [Test]
        public void CalculatesCreatedResourcesCorrectly()
        {
            var retriever = new MockResourceRetriever();
            var log = new InMemoryLog();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, log);

            var resources = ResourceFactory.FromListJson(ResourceLoader.Load("two-deployments.json"));
            retriever.SetOutput(resources);

            var identifiers = new[]
            {
                new ResourceIdentifier
                {
                    Kind = "Deployment",
                    Name = "nginx",
                    Namespace = "test"
                },
                new ResourceIdentifier
                {
                    Kind = "Deployment",
                    Name = "redis",
                    Namespace = "test"
                },
            };
            
            resourceStatusChecker.UpdateResourceStatuses(identifiers, null);

            log.Messages.Where(m => m.FormattedMessage.Contains(SpecialVariables.KubernetesResourceStatusServiceMessageName))
                .Should().HaveCount(2);
        }
        
        [Test]
        public void CalculatesUpdatedResourcesCorrectly()
        {
            var retriever = new MockResourceRetriever();
            var log = new InMemoryLog();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, log);

            var oldResources = ResourceFactory.FromListJson(ResourceLoader.Load("two-deployments.json"));
            retriever.SetOutput(oldResources);

            var identifiers = new[]
            {
                new ResourceIdentifier
                {
                    Kind = "Deployment",
                    Name = "nginx",
                    Namespace = "test"
                },
                new ResourceIdentifier
                {
                    Kind = "Deployment",
                    Name = "redis",
                    Namespace = "test"
                },
            };
            
            resourceStatusChecker.UpdateResourceStatuses(identifiers, null);
            log.Messages.Clear();
            
            var newResources = ResourceFactory.FromListJson(ResourceLoader.Load("one-old-deployment-and-one-new-deployment.json"));
            retriever.SetOutput(newResources);
            resourceStatusChecker.UpdateResourceStatuses(identifiers, null);
            
            log.Messages
                .Where(m => m.FormattedMessage.Contains(SpecialVariables.KubernetesResourceStatusServiceMessageName))
                .Should().HaveCount(1);
        }
        
        [Test]
        public void CalculatesRemovedResourcesCorrectly()
        {
            var retriever = new MockResourceRetriever();
            var log = new InMemoryLog();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, log);

            var oldResources = ResourceFactory.FromListJson(ResourceLoader.Load("two-deployments.json"));
            retriever.SetOutput(oldResources);

            var identifiers = new[]
            {
                new ResourceIdentifier
                {
                    Kind = "Deployment",
                    Name = "nginx",
                    Namespace = "test"
                },
                new ResourceIdentifier
                {
                    Kind = "Deployment",
                    Name = "redis",
                    Namespace = "test"
                },
            };
            
            resourceStatusChecker.UpdateResourceStatuses(identifiers, null);
            log.Messages.Clear();
            
            var newResources = ResourceFactory.FromListJson(ResourceLoader.Load("one-deployment.json"));
            retriever.SetOutput(newResources);
            resourceStatusChecker.UpdateResourceStatuses(identifiers, null);
            
            log.Messages
                .Where(m => m.FormattedMessage.Contains(SpecialVariables.KubernetesResourceStatusServiceMessageName))
                .Should().HaveCount(1);
        }
    }

    /// <summary>
    /// MockResourceRetriever returns whatever resources are set via the SetOutput method
    /// </summary>
    public class MockResourceRetriever : IResourceRetriever
    {
        private List<Resource> resources;

        public void SetOutput(IEnumerable<Resource> resources)
        {
            this.resources = resources.ToList();
        }
        
        public IEnumerable<Resource> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, IKubectl kubectl)
        {
            return resources.ToList();
        }
    }
}