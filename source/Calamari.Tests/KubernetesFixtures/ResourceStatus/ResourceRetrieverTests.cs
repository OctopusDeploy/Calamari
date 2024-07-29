using System;
using System.Collections.Generic;
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

            var nginxDeployment = new ResourceResponseBuilder()
                .WithKind("Deployment")
                .WithName("nginx")
                .WithUid(deploymentUid)
                .Build();

            var nginxReplicaSet = new ResourceResponseBuilder()
                .WithKind("ReplicaSet")
                .WithName("nginx-replicaset")
                .WithUid(replicaSetUid)
                .WithOwnerUid(deploymentUid)
                .Build();

            var pod1 = new ResourceResponseBuilder()
                .WithKind("Pod")
                .WithName("nginx-pod-1")
                .WithOwnerUid(replicaSetUid)
                .Build();

            var pod2 = new ResourceResponseBuilder()
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
                    new ResourceIdentifier("Deployment", "nginx", "octopus")
                },
                null, new Options());

            got.Should().BeEquivalentTo(new object[]
            {
                new
                {
                    Kind = "Deployment",
                    Name = "nginx",
                    Children = new object[]
                    {
                        new
                        {
                            Kind = "ReplicaSet",
                            Name = "nginx-replicaset",
                            Children = new object[]
                            {
                                new { Kind = "Pod", Name = "nginx-pod-1"},
                                new { Kind = "Pod", Name = "nginx-pod-2"},
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

            var deployment1 = new ResourceResponseBuilder()
                .WithKind("Deployment")
                .WithName("deployment-1")
                .WithUid(deployment1Uid)
                .Build();

            var replicaSet1 = new ResourceResponseBuilder()
                .WithKind("ReplicaSet")
                .WithName("replicaset-1")
                .WithOwnerUid(deployment1Uid)
                .Build();

            var deployment2 = new ResourceResponseBuilder()
                .WithKind("Deployment")
                .WithName("deployment-2")
                .WithUid(deployment2Uid)
                .Build();

            var replicaSet2 = new ResourceResponseBuilder()
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
                    new ResourceIdentifier("Deployment", "deployment-1", "octopus"),
                    new ResourceIdentifier("Deployment", "deployment-2", "octopus")
                },
                null, new Options());

            got.Should().BeEquivalentTo(new object[]
            {
                new
                {
                    Kind = "Deployment",
                    Name = "deployment-1",
                    Children = new object[]
                    {
                        new
                        {
                            Kind = "ReplicaSet",
                            Name = "replicaset-1",
                        }
                    }
                },
                new
                {
                    Kind = "Deployment",
                    Name = "deployment-2",
                    Children = new object[]
                    {
                        new
                        {
                            Kind = "ReplicaSet",
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

            var replicaSet = new ResourceResponseBuilder()
                .WithKind("ReplicaSet")
                .WithName("rs")
                .WithUid(replicaSetUid)
                .Build();

            var childPod = new ResourceResponseBuilder()
                .WithKind("Pod")
                .WithName("pod-1")
                .WithOwnerUid(replicaSetUid)
                .Build();

            var irrelevantPod = new ResourceResponseBuilder()
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
                    new ResourceIdentifier("ReplicaSet", "rs", "octopus"),
                },
                null, new Options());

            got.Should().BeEquivalentTo(new object[]
            {
                new
                {
                    Kind = "ReplicaSet",
                    Name = "rs",
                    Children = new object[]
                    {
                        new { Kind = "Pod", Name = "pod-1" }
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


        public string Resource(string kind, string name, string @namespace, IKubectl kubectl)
        {
            return resourceEntries[name];
        }

        public string AllResources(string kind, string @namespace, IKubectl kubectl)
        {
            return resourcesByKind[kind];
        }
    }

    public class ResourceResponseBuilder
    {
        private static string template = @"
{{
    ""kind"": ""{0}"",
    ""metadata"": {{
        ""name"": ""{1}"",
        ""uid"": ""{2}"",
        ""ownerReferences"": [
            {{
                ""uid"": ""{3}""
            }}
        ]
    }}
}}";

        private string kind = "";
        private string name = "";
        private string uid = Guid.NewGuid().ToString();
        private string ownerUid = Guid.NewGuid().ToString();

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

        public string Build() => string.Format(template, kind, name, uid, ownerUid).ReplaceLineEndings();
    }
}