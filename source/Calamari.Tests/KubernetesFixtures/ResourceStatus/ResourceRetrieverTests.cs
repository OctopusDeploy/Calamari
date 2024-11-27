using System;
using System.Collections.Generic;
using Amazon.IdentityManagement.Model;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
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


            var got = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "nginx", "octopus")
                },
                null, new Options());

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

            var got = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "deployment-1", "octopus"),
                    new ResourceIdentifier(SupportedResourceGroupVersionKinds.DeploymentV1, "deployment-2", "octopus")
                },
                null, new Options());

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


            var got = resourceRetriever.GetAllOwnedResources(
                new List<ResourceIdentifier>
                {
                    new ResourceIdentifier(SupportedResourceGroupVersionKinds.ReplicaSetV1, "rs", "octopus"),
                },
                null, new Options());

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
    }

    public class MockKubectlGet : IKubectlGet
    {
        private readonly Dictionary<string, string> resourceEntries = new Dictionary<string, string>();
        private readonly Dictionary<string, string> resourcesByKind = new Dictionary<string, string>();

        public void SetResource(string name, string data)
        {
            resourceEntries.Add(name, data);
        }

        public void SetAllResources(string kind, params string[] data)
        {
            resourcesByKind.Add(kind, $"{{items: [{string.Join(",", data)}]}}");
        }


        public KubectlGetResult Resource(IResourceIdentity resourceIdentity, IKubectl kubectl)
        {
            return new KubectlGetResult(resourceEntries[resourceIdentity.Name], new List<string>
            {
                $"{Level.Info}: {resourceEntries[resourceIdentity.Name]}"
            });
        }

        public KubectlGetResult AllResources(ResourceGroupVersionKind groupVersionKind, string @namespace, IKubectl kubectl)
        {
            return new KubectlGetResult(resourcesByKind[groupVersionKind.Kind], new List<string>
            {
                $"{Level.Info}: {resourcesByKind[groupVersionKind.Kind]}"
            });
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