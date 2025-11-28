using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    [TestFixture]
    public class ResourceRetrieverTests
    {
        [Test]
        public void ReturnsCorrectObjectHierarchyForDeployments()
        {
            var deploymentUid = Guid.NewGuid().ToString();
            var replicaSetUid = Guid.NewGuid().ToString();

            var nginxDeployment = new ResourceResponseBuilder().WithApiVersion("apps/v1")
                .WithKind("Deployment")
                .WithName("nginx")
                .WithUid(deploymentUid)
                .Build();

            var nginxReplicaSet = new ResourceResponseBuilder().WithApiVersion("apps/v1")
                .WithKind("ReplicaSet")
                .WithName("nginx-replicaset")
                .WithUid(replicaSetUid)
                .WithOwnerUid(deploymentUid)
                .Build();

            var pod1 = new ResourceResponseBuilder().WithApiVersion("v1")
                .WithKind("Pod")
                .WithName("nginx-pod-1")
                .WithOwnerUid(replicaSetUid)
                .Build();

            var pod2 = new ResourceResponseBuilder().WithApiVersion("v1")
                .WithKind("Pod")
                .WithName("nginx-pod-2")
                .WithOwnerUid(replicaSetUid)
                .Build();

            var kubectlGet = new MockKubectlGet();
            var resourceRetriever = new ResourceRetriever(kubectlGet, Substitute.For<ILog>());

            kubectlGet.SetResource("nginx", nginxDeployment);
            kubectlGet.SetAllResources("ReplicaSet", nginxReplicaSet);
            kubectlGet.SetAllResources("Pod", pod1, pod2);


            var result = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "nginx", "octopus")
                },
                null, new Options());
            var got = result.Select(r => r.Value);

            got.Should().BeEquivalentTo(new object[]
            {
                new
                {
                    GroupVersionKind = SupportedResourceGroupVersionKinds.DeploymentV1,
                    Name = "nginx",
                    Children = new object[]
                    {
                        new
                        {
                            GroupVersionKind = SupportedResourceGroupVersionKinds.ReplicaSetV1,
                            Name = "nginx-replicaset",
                            Children = new object[]
                            {
                                new { GroupVersionKind = SupportedResourceGroupVersionKinds.PodV1, Name = "nginx-pod-1"},
                                new { GroupVersionKind = SupportedResourceGroupVersionKinds.PodV1, Name = "nginx-pod-2"},
                            }
                        }
                    }
                }
            });
        }

        [Test]
        public void ReturnsCorrectObjectHierarchyForMultipleResources()
        {
            var deployment1Uid = Guid.NewGuid().ToString();
            var deployment2Uid = Guid.NewGuid().ToString();

            var deployment1 = new ResourceResponseBuilder().WithApiVersion("apps/v1")
                .WithKind("Deployment")
                .WithName("deployment-1")
                .WithUid(deployment1Uid)
                .Build();

            var replicaSet1 = new ResourceResponseBuilder().WithApiVersion("apps/v1")
                .WithKind("ReplicaSet")
                .WithName("replicaset-1")
                .WithOwnerUid(deployment1Uid)
                .Build();

            var deployment2 = new ResourceResponseBuilder().WithApiVersion("apps/v1")
                .WithKind("Deployment")
                .WithName("deployment-2")
                .WithUid(deployment2Uid)
                .Build();

            var replicaSet2 = new ResourceResponseBuilder().WithApiVersion("apps/v1")
                .WithKind("ReplicaSet")
                .WithName("replicaset-2")
                .WithOwnerUid(deployment2Uid)
                .Build();

            var kubectlGet = new MockKubectlGet();
            var resourceRetriever = new ResourceRetriever(kubectlGet, Substitute.For<ILog>());

            kubectlGet.SetResource("deployment-1", deployment1);
            kubectlGet.SetResource("deployment-2", deployment2);
            kubectlGet.SetAllResources("ReplicaSet", replicaSet1, replicaSet2);
            kubectlGet.SetAllResources("Pod");

            var result = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "deployment-1", "octopus"),
                    new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "deployment-2", "octopus")
                },
                null, new Options());
            var got = result.Select(r => r.Value);

            got.Should().BeEquivalentTo(new object[]
            {
                new
                {
                    GroupVersionKind = SupportedResourceGroupVersionKinds.DeploymentV1,
                    Name = "deployment-1",
                    Children = new object[]
                    {
                        new
                        {
                            GroupVersionKind = SupportedResourceGroupVersionKinds.ReplicaSetV1,
                            Name = "replicaset-1",
                        }
                    }
                },
                new
                {
                    GroupVersionKind = SupportedResourceGroupVersionKinds.DeploymentV1,
                    Name = "deployment-2",
                    Children = new object[]
                    {
                        new
                        {
                            GroupVersionKind = SupportedResourceGroupVersionKinds.ReplicaSetV1,
                            Name = "replicaset-2",
                        }
                    }
                }
            });
        }

        [Test]
        public void IgnoresIrrelevantResources()
        {
            var replicaSetUid = Guid.NewGuid().ToString();

            var replicaSet = new ResourceResponseBuilder().WithApiVersion("apps/v1")
                .WithKind("ReplicaSet")
                .WithName("rs")
                .WithUid(replicaSetUid)
                .Build();

            var childPod = new ResourceResponseBuilder().WithApiVersion("v1")
                .WithKind("Pod")
                .WithName("pod-1")
                .WithOwnerUid(replicaSetUid)
                .Build();

            var irrelevantPod = new ResourceResponseBuilder().WithApiVersion("v1")
                .WithKind("Pod")
                .WithName("pod-x")
                .Build();

            var kubectlGet = new MockKubectlGet();
            var resourceRetriever = new ResourceRetriever(kubectlGet, Substitute.For<ILog>());

            kubectlGet.SetResource("rs", replicaSet);
            kubectlGet.SetAllResources("Pod", childPod, irrelevantPod);


            var result = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier(SupportedResourceGroupVersionKinds.ReplicaSetV1, "rs", "octopus"),
                },
                null, new Options());
            var got = result.Select(r => r.Value);

            got.Should().BeEquivalentTo(new object[]
            {
                new
                {
                    GroupVersionKind = SupportedResourceGroupVersionKinds.ReplicaSetV1,
                    Name = "rs",
                    Children = new object[]
                    {
                        new { GroupVersionKind = SupportedResourceGroupVersionKinds.PodV1, Name = "pod-1" }
                    }
                }
            });
        }

        [Test]
        public void HandlesInvalidJson()
        {
            var kubectlGet = new MockKubectlGet();
            var resourceRetriever = new ResourceRetriever(kubectlGet, Substitute.For<ILog>());

            kubectlGet.SetResource("rs", "invalid json");
            var results = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier(SupportedResourceGroupVersionKinds.ReplicaSetV1, "rs", "octopus"),
                },
                null, new Options());

            var result = results.Should().ContainSingle().Which;
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Failed to parse JSON");
        }

        [Test]
        public void HandlesGetErrors()
        {
            var kubectlGet = new MockKubectlGet();
            var resourceRetriever = new ResourceRetriever(kubectlGet, Substitute.For<ILog>());

            Message[] messages = { new Message(Level.Error, "Error getting resource") };
            kubectlGet.SetResource("rs", messages);
            var results = resourceRetriever.GetAllOwnedResources(
                 new List<ResourceIdentifier>
                 {
                     new ResourceIdentifier(SupportedResourceGroupVersionKinds.ReplicaSetV1, "rs", "octopus"),
                 },
                 null,
                 new Options()
                 {
                     PrintVerboseKubectlOutputOnError = true
                 });

            var result = results.Should().ContainSingle().Which;
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Error getting resource");
        }


        [Test]
        public void HandlesEmptyResponse()
        {
            var kubectlGet = new MockKubectlGet();
            var resourceRetriever = new ResourceRetriever(kubectlGet, Substitute.For<ILog>());

            kubectlGet.SetResource("rs", Array.Empty<Message>());
            var results = resourceRetriever.GetAllOwnedResources(
                 new List<ResourceIdentifier>
                 {
                     new ResourceIdentifier(SupportedResourceGroupVersionKinds.ReplicaSetV1, "rs", "octopus"),
                 },
                 null, new Options());

            var result = results.Should().ContainSingle().Which;
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Failed to get resource");
        }

        [Test]
        public void HandleChildFailure()
        {
            var replicaSetUid = Guid.NewGuid().ToString();

            var replicaSet = new ResourceResponseBuilder().WithApiVersion("apps/v1")
                .WithKind("ReplicaSet")
                .WithName("rs")
                .WithUid(replicaSetUid)
                .Build();

            var kubectlGet = new MockKubectlGet();
            var log = new InMemoryLog();
            var resourceRetriever = new ResourceRetriever(kubectlGet, log);

            kubectlGet.SetResource("rs", replicaSet);
            Message[] messages = { new Message(Level.Error, "Error getting resource") };
            kubectlGet.SetAllResources("Pod", messages);

            var results = resourceRetriever.GetAllOwnedResources(
                 new List<ResourceIdentifier>
                 {
                     new ResourceIdentifier(SupportedResourceGroupVersionKinds.ReplicaSetV1, "rs", "octopus"),
                 },
                 null,
                 new Options()
                 {
                     PrintVerboseKubectlOutputOnError = true
                 });


            var result = results.Should().ContainSingle().Which;
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEquivalentTo(new
                {
                    GroupVersionKind = SupportedResourceGroupVersionKinds.ReplicaSetV1,
                    Name = "rs",
                    Children = Array.Empty<object>(),
                }
            );

            log.MessagesVerboseFormatted
               .Should()
               .Contain(r => r.Contains("Error getting resource"));
        }

        [Test]
        public void HandlesKubectlFailureWithExitCode()
        {
            var kubectlGet = new MockKubectlGet();
            kubectlGet.SetResource("deploy", new[] {
                new Message(Level.Error, "Error from server (Forbidden): deployments.apps \"deploy\" is forbidden")
            });

            var log = new InMemoryLog();
            var resourceRetriever = new ResourceRetriever(kubectlGet, log);

            var results = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "deploy", "default")
                },
                null,
                new Options { PrintVerboseKubectlOutputOnError = true }
            ).ToList();

            log.MessagesVerboseFormatted
                .Should()
                .Contain(msg => msg.Contains("kubectl failed with exit code: 1"));

            log.MessagesVerboseFormatted
                .Should()
                .Contain(msg => msg.Contains("Error from server (Forbidden)"));
        }
    }


    public class MockKubectlGet : IKubectlGet
    {
        private readonly Dictionary<string, Message[]> resourceEntries = new Dictionary<string, Message[]>();
        private readonly Dictionary<string, Message[]> resourcesByKind = new Dictionary<string, Message[]>();

        public void SetResource(string name, string data)
        {
            Message[] messages = { new Message(Level.Info, data) };
            resourceEntries.Add(name, messages);
        }
        public void SetResource(string name, Message[] messages)
        {
            resourceEntries.Add(name, messages);
        }

        public void SetAllResources(string kind, params string[] data)
        {
            Message[] messages = { new Message(Level.Info, $"{{items: [{string.Join(",", data)}]}}") };
            resourcesByKind.Add(kind, messages);
        }

        public void SetAllResources(string kind, Message[] messages)
        {
            resourcesByKind.Add(kind, messages);
        }


        public KubectlGetResult Resource(IResourceIdentity resourceIdentity, IKubectl kubectl)
        {
            var resourceJson = resourceEntries[resourceIdentity.Name].Select(m => m.Text).Join(string.Empty);
            var rawOutput = resourceEntries[resourceIdentity.Name].Select(m => $"{m.Level}: {m.Text}").ToList();
            var exitCode = resourceEntries[resourceIdentity.Name].Any(m => m.Level == Level.Error) ? 1 : 0;

            return new KubectlGetResult(resourceJson, rawOutput, exitCode);
        }

        public KubectlGetResult AllResources(ResourceGroupVersionKind groupVersionKind, string @namespace, IKubectl kubectl)
        {
            var resourceJson = resourcesByKind[groupVersionKind.Kind].Select(m => m.Text).Join(string.Empty);
            var rawOutput = resourcesByKind[groupVersionKind.Kind].Select(m => $"{m.Level}: {m.Text}").ToList();
            var exitCode = resourcesByKind[groupVersionKind.Kind].Any(m => m.Level == Level.Error) ? 1 : 0;

            return new KubectlGetResult(resourceJson, rawOutput, exitCode);
        }
    }

    public class ResourceResponseBuilder
    {
        static string template = @"
{{
    ""apiVersion"": ""{0}"",
    ""kind"": ""{1}"",
    ""metadata"": {{
        ""name"": ""{2}"",
        ""uid"": ""{3}"",
        ""ownerReferences"": [
            {{
                ""uid"": ""{4}""
            }}
        ]
    }}
}}";

        string apiVersion = "";
        string kind = "";
        string name = "";
        string uid = Guid.NewGuid().ToString();
        string ownerUid = Guid.NewGuid().ToString();

        public ResourceResponseBuilder WithApiVersion(string apiVersion)
        {
            this.apiVersion = apiVersion;
            return this;
        }

        public ResourceResponseBuilder WithKind(string kind)
        {
            this.kind = kind;
            return this;
        }

        public ResourceResponseBuilder WithName(string name)
        {
            this.name = name;
            return this;
        }

        public ResourceResponseBuilder WithUid(string uid)
        {
            this.uid = uid;
            return this;
        }

        public ResourceResponseBuilder WithOwnerUid(string ownerUid)
        {
            this.ownerUid = ownerUid;
            return this;
        }

        public string Build() =>
            string.Format(template,
                          apiVersion,
                          kind,
                          name,
                          uid,
                          ownerUid)
                  .ReplaceLineEndings()
                  .Replace(Environment.NewLine, string.Empty);
    }
}
