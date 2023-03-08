using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    [TestFixture]
    public class ResourceUpdateReporterTests
    {
        [Test]
        public void ReportsCreatedResourcesCorrectly()
        {
            var variables = new CalamariVariables();
            var log = new InMemoryLog();
            
            var reporter = new ResourceUpdateReporter(variables, log);
            
            var originalStatuses = new Dictionary<string, Resource>();
            var newStatuses = ResourceFactory
                .FromListJson(TestFileLoader.Load("two-deployments.json")) 
                .ToDictionary(resource => resource.Uid, resource => resource);
            
            reporter.ReportUpdatedResources(originalStatuses, newStatuses);

            var serviceMessages = log.ServiceMessages
                .Where(message => message.Name == SpecialVariables.KubernetesResourceStatusServiceMessageName)
                .ToList();

            serviceMessages.Should().HaveCount(2);
            foreach (var message in serviceMessages)
            {
                message.Properties["removed"].Should().Be(false.ToString());
            }
        }
        
        [Test]
        public void ReportsUpdatedResourcesCorrectly()
        {
            var variables = new CalamariVariables();
            var log = new InMemoryLog();
            
            var reporter = new ResourceUpdateReporter(variables, log);
            
            var originalStatuses = ResourceFactory
                .FromListJson(TestFileLoader.Load("two-deployments.json"))
                .ToDictionary(resource => resource.Uid, resource => resource);
            var newStatuses = ResourceFactory
                .FromListJson(TestFileLoader.Load("one-old-deployment-and-one-new-deployment.json"))
                .ToDictionary(resource => resource.Uid, resource => resource);
            
            reporter.ReportUpdatedResources(originalStatuses, newStatuses);
            
            var serviceMessages = log.ServiceMessages
                .Where(message => message.Name == SpecialVariables.KubernetesResourceStatusServiceMessageName)
                .ToList();

            serviceMessages.Should().HaveCount(1);
            var updated = serviceMessages.First();
            updated.Properties["removed"].Should().Be(false.ToString());
            updated.Properties["name"].Should().Be("nginx");
        }
        
        [Test]
        public void ReportsRemovedResourcesCorrectly()
        {
            var variables = new CalamariVariables();
            var log = new InMemoryLog();
            
            var reporter = new ResourceUpdateReporter(variables, log);
            
            var originalStatuses = ResourceFactory
                .FromListJson(TestFileLoader.Load("two-deployments.json"))
                .ToDictionary(resource => resource.Uid, resource => resource);
            var newStatuses = ResourceFactory
                .FromListJson(TestFileLoader.Load("one-deployment.json"))
                .ToDictionary(resource => resource.Uid, resource => resource);
            
            reporter.ReportUpdatedResources(originalStatuses, newStatuses);
            
            var serviceMessages = log.ServiceMessages
                .Where(message => message.Name == SpecialVariables.KubernetesResourceStatusServiceMessageName)
                .ToList();

            serviceMessages.Should().HaveCount(1);
            var removed = serviceMessages.First();
            removed.Properties["removed"].Should().Be(true.ToString());
            removed.Properties["name"].Should().Be("redis");
        }
    }
}