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
                .FromListJson(TestFileLoader.Load("two-deployments.json"), new Options()) 
                .ToDictionary(resource => resource.Uid, resource => resource);
            
            reporter.ReportUpdatedResources(originalStatuses, newStatuses, 1);

            var serviceMessages = log.ServiceMessages
                .Where(message => message.Name == SpecialVariables.ServiceMessages.ResourceStatus.Name)
                .ToList();

            serviceMessages.Select(message => message.Properties["name"])
                .Should().BeEquivalentTo(new string[]
                {
                    "nginx",
                    "redis"
                });
            
            serviceMessages.Select(message => message.Properties["removed"])
                .Should().BeEquivalentTo(new string[]
                {
                    bool.FalseString,
                    bool.FalseString
                });
            
            serviceMessages.Select(message => message.Properties["checkCount"])
                .Should().BeEquivalentTo(new string[]
                {
                    "1",
                    "1"
                });
        }
        
        [Test]
        public void ReportsUpdatedResourcesCorrectly()
        {
            var variables = new CalamariVariables();
            var log = new InMemoryLog();
            
            var reporter = new ResourceUpdateReporter(variables, log);
            
            var originalStatuses = ResourceFactory
                .FromListJson(TestFileLoader.Load("two-deployments.json"), new Options())
                .ToDictionary(resource => resource.Uid, resource => resource);
            var newStatuses = ResourceFactory
                .FromListJson(TestFileLoader.Load("one-old-deployment-and-one-new-deployment.json"), new Options())
                .ToDictionary(resource => resource.Uid, resource => resource);
            
            reporter.ReportUpdatedResources(originalStatuses, newStatuses, 1);
            
            var serviceMessages = log.ServiceMessages
                .Where(message => message.Name == SpecialVariables.ServiceMessages.ResourceStatus.Name)
                .ToList();

            serviceMessages.Should().ContainSingle().Which.Properties
                .Should().Contain(new KeyValuePair<string, string>[]
                {
                    new KeyValuePair<string, string>("name", "nginx"),
                    new KeyValuePair<string, string>("removed", bool.FalseString),
                    new KeyValuePair<string, string>("checkCount", "1")
                });
        }
        
        [Test]
        public void ReportsRemovedResourcesCorrectly()
        {
            var variables = new CalamariVariables();
            var log = new InMemoryLog();
            
            var reporter = new ResourceUpdateReporter(variables, log);
            
            var originalStatuses = ResourceFactory
                .FromListJson(TestFileLoader.Load("two-deployments.json"), new Options())
                .ToDictionary(resource => resource.Uid, resource => resource);
            var newStatuses = ResourceFactory
                .FromListJson(TestFileLoader.Load("one-deployment.json"), new Options())
                .ToDictionary(resource => resource.Uid, resource => resource);
            
            reporter.ReportUpdatedResources(originalStatuses, newStatuses, 1);
            
            var serviceMessages = log.ServiceMessages
                .Where(message => message.Name == SpecialVariables.ServiceMessages.ResourceStatus.Name)
                .ToList();

            serviceMessages.Should().ContainSingle().Which.Properties
                .Should().Contain(new KeyValuePair<string, string>[]
                {
                    new KeyValuePair<string, string>("name", "redis"),
                    new KeyValuePair<string, string>("removed", bool.TrueString),
                    new KeyValuePair<string, string>("checkCount", "1"),
                    new KeyValuePair<string, string>("group", "apps"),
                    new KeyValuePair<string, string>("version", "v1"),
                    new KeyValuePair<string, string>("kind", "Deployment"),
                });
        }
        
        [Test]
        public void ClusterScopedResourcesAreIncluded()
        {
            var variables = new CalamariVariables();
            var log = new InMemoryLog();
            
            var reporter = new ResourceUpdateReporter(variables, log);
            
            var newStatuses = ResourceFactory
                .FromListJson(TestFileLoader.Load("one-deployment-and-one-namespace.json"), new Options())
                .ToDictionary(resource => resource.Uid, resource => resource);
            
            reporter.ReportUpdatedResources(new Dictionary<string, Resource>(), newStatuses, 1);
            
            var serviceMessages = log.ServiceMessages
                .Where(message => message.Name == SpecialVariables.ServiceMessages.ResourceStatus.Name)
                .ToList();
            
            serviceMessages.Should().Contain(sm => sm.Properties.Intersect(new[]
            {
                new KeyValuePair<string, string>("name", "app"),
                new KeyValuePair<string, string>("checkCount", "1"),
                new KeyValuePair<string, string>("group", "apps"),
                new KeyValuePair<string, string>("version", "v1"),
                new KeyValuePair<string, string>("kind", "Deployment"),
                new KeyValuePair<string, string>("namespace", "default"),
            }).Count() == 6);
            
            serviceMessages.Should().Contain(sm => sm.Properties.Intersect(new[]
            {
                new KeyValuePair<string, string>("name", "my-namespace"),
                new KeyValuePair<string, string>("checkCount", "1"),
                new KeyValuePair<string, string>("group", string.Empty),
                new KeyValuePair<string, string>("version", "v1"),
                new KeyValuePair<string, string>("kind", "Namespace"),
                new KeyValuePair<string, string>("namespace", null),
            }).Count() == 6);
        }
    }
}