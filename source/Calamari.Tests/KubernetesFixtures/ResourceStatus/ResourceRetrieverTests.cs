using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Octopus.CoreUtilities;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    [TestFixture]
    public class ResourceRetrieverTests
    {
        [Test]
        public void ReturnsCorrectObjectHierarchyForDeployments()
        {
            var kubectl = new MockKubectl(TestFileLoader.Load("deployment-with-3-replicas.json"));
            var resourceRetriever = new ResourceRetriever();

            var got = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier{ Kind = "Deployment", Name = "nginx" }
                },
                kubectl);
    
            got.Should().HaveCount(5);
            got.Where(resource => resource.Kind == "Deployment").Should().HaveCount(1);
            got.Where(resource => resource.Kind == "ReplicaSet").Should().HaveCount(1);
            got.Where(resource => resource.Kind == "Pod").Should().HaveCount(3);
            got.First(resource => resource.Kind == "Deployment").Children.Should().HaveCount(1);
            got.First(resource => resource.Kind == "ReplicaSet").Children.Should().HaveCount(3);
        }
    
        [Test]
        public void ReturnsCorrectObjectHierarchyForMultipleResources()
        {
            var kubectl = new MockKubectl(TestFileLoader.Load("2-deployments-with-3-replicas-each.json"));
            var resourceRetriever = new ResourceRetriever();
    
            var got = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier { Kind = "Deployment", Name = "nginx" },
                    new ResourceIdentifier { Kind = "Deployment", Name = "curl" }
                },
                kubectl);
    
            got.Should().HaveCount(10);
            got.Where(resource => resource.Kind == "Deployment").Should().HaveCount(2);
            got.Where(resource => resource.Kind == "ReplicaSet").Should().HaveCount(2);
            got.Where(resource => resource.Kind == "Pod").Should().HaveCount(6);
        }
    }
    
    /// <summary>
    /// MockKubectl loads a JSON file that contains a list of kubernetes objects,
    /// and sets up ExecuteCommandAndReturnOutput method to respond to
    /// 'kubectl get [kind] [name]' and 'kubectl get [kind]' calls.
    /// It ignores other invocations.
    /// </summary>
    public class MockKubectl : IKubectl
    {
        private readonly IEnumerable<JObject> data;

        public MockKubectl(string json)
        {
            data = JArray.Parse(json).Cast<JObject>();
        }

        public bool TrySetKubectl() => true;
        
        public IEnumerable<string> ExecuteCommandAndReturnOutput(params string[] arguments)
        {
            // We only need to cater for "kubectl get <kind> <name>" and "kubectl get <kind>"
            
            if (arguments.Length < 2 || arguments[0] != "get")
            {
                yield break;
            }

            var kind = arguments[1];
            var nameProvided = arguments.Length >= 3 && !arguments[2].StartsWith("-");
            
            if (!nameProvided)
            {
                // when resource name is not provided, return all resources of that kind in a List response
                var items = new JArray(data.Where(item => item.SelectToken("$.kind").Value<string>() == kind));
                yield return $"{{items: {items}}}" ;
            }
            else
            {
                // when resource name is provided, return that resource
                var name = arguments[2];
                yield return data
                    .FirstOrDefault(item => 
                        item.SelectToken("$.kind").Value<string>() == kind 
                        && item.SelectToken("$.metadata.name").Value<string>() == name)
                    ?.ToString();
            }
        }

        public CommandResult ExecuteCommand(params string[] arguments) => new CommandResult("kubectl", 0);

        public void ExecuteCommandAndAssertSuccess(params string[] arguments)
        {
        }
        
        public Maybe<SemanticVersion> GetVersion()
        {
            return Maybe<SemanticVersion>.None;
        }
    }
}