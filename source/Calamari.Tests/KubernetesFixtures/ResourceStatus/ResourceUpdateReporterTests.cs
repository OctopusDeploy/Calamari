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
        public void CalculatesCreatedResourcesCorrectly()
        {
            var variables = new CalamariVariables();
            var log = new InMemoryLog();
            
            var reporter = new ResourceUpdateReporter(variables, log);
            
            var originalStatuses = new Dictionary<string, Resource>();
            var newStatuses = ResourceFactory.FromListJson(TestFileLoader.Load("two-deployments.json"))
                .ToDictionary(resource => resource.Uid, resource => resource);
            
            reporter.ReportUpdatedResources(originalStatuses, newStatuses);

            log.Messages.Where(m => m.FormattedMessage.Contains(SpecialVariables.KubernetesResourceStatusServiceMessageName))
                .Should().HaveCount(2);
        }
        
        [Test]
        public void CalculatesUpdatedResourcesCorrectly()
        {
            var variables = new CalamariVariables();
            var log = new InMemoryLog();
            
            var reporter = new ResourceUpdateReporter(variables, log);
            
            var originalStatuses = ResourceFactory.FromListJson(TestFileLoader.Load("two-deployments.json"))
                .ToDictionary(resource => resource.Uid, resource => resource);
            var newStatuses = ResourceFactory.FromListJson(TestFileLoader.Load("one-old-deployment-and-one-new-deployment.json"))
                .ToDictionary(resource => resource.Uid, resource => resource);
            
            reporter.ReportUpdatedResources(originalStatuses, newStatuses);
            
            log.Messages
                .Where(m => m.FormattedMessage.Contains(SpecialVariables.KubernetesResourceStatusServiceMessageName))
                .Should().HaveCount(1);
        }
        
        [Test]
        public void CalculatesRemovedResourcesCorrectly()
        {
            var variables = new CalamariVariables();
            var log = new InMemoryLog();
            
            var reporter = new ResourceUpdateReporter(variables, log);
            
            var originalStatuses = ResourceFactory.FromListJson(TestFileLoader.Load("two-deployments.json"))
                .ToDictionary(resource => resource.Uid, resource => resource);
            var newStatuses = ResourceFactory.FromListJson(TestFileLoader.Load("one-deployment.json"))
                .ToDictionary(resource => resource.Uid, resource => resource);
            
            reporter.ReportUpdatedResources(originalStatuses, newStatuses);
            
            log.Messages
                .Where(m => m.FormattedMessage.Contains(SpecialVariables.KubernetesResourceStatusServiceMessageName))
                .Should().HaveCount(1);
        }
    }
}