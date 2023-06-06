using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    
    [TestFixture]
    public class ResourceStatusCheckerTests
    {
        [Test]
        public void ShouldReportStatusesWithIncrementingCheckCount()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, reporter, new InMemoryLog());

            var timer = new TestTimer(5);

            for (var i = 0; i < 5; i++)
            {
                retriever.SetResponses(new List<Resource>{ new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress) });
            }
            
            resourceStatusChecker.CheckStatusUntilCompletionOrTimeout(
                new ResourceIdentifier[]
                {
                    new ResourceIdentifier("Pod", "my-pod", "default")
                }, timer, null, new Options());
            
            reporter.CheckCounts().Should().BeEquivalentTo(new List<int>() { 1, 2, 3, 4, 5 });
        }

        [Test]
        public void SuccessfulBeforeTimeout_ShouldBeSuccessful()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var log = new InMemoryLog();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, reporter, log);

            var timer = new TestTimer(2);

            retriever.SetResponses(
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful) }
            );
            
            var result = resourceStatusChecker.CheckStatusUntilCompletionOrTimeout(
                new ResourceIdentifier[]
                {
                    new ResourceIdentifier("Pod", "my-pod", "default")
                }, timer, null, new Options());

            result.Should().BeTrue();
            log.StandardError.Should().BeEmpty();
            log.StandardOut.Should().Contain(ResourceStatusChecker.MessageDeploymentSucceeded);
        }

        [Test]
        public void FailureBeforeTimeout_ShouldLogFailure()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var log = new InMemoryLog();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, reporter, log);

            var timer = new TestTimer(2);

            retriever.SetResponses(
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Failed) }
            );
            
            var result = resourceStatusChecker.CheckStatusUntilCompletionOrTimeout(
                new ResourceIdentifier[]
                {
                    new ResourceIdentifier("Pod", "my-pod", "default")
                }, timer, null, new Options());

            result.Should().BeFalse();
            log.StandardError
                .Should().ContainSingle().Which
                .Should().Be(ResourceStatusChecker.MessageDeploymentFailed);
        }

        [Test]
        public void DeploymentInProgressAtTheEndOfTimeout_ShouldLogFailure()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var log = new InMemoryLog();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, reporter, log);

            var timer = new TestTimer(2);

            retriever.SetResponses(
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress) },
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress) }
            );
            
            var result = resourceStatusChecker.CheckStatusUntilCompletionOrTimeout(
                new ResourceIdentifier[]
                {
                    new ResourceIdentifier("Pod", "my-pod", "default")
                }, timer, null, new Options());

            result.Should().BeFalse();
            log.StandardError
                .Should().ContainSingle().Which
                .Should().Be(ResourceStatusChecker.MessageInProgressAtTheEndOfTimeout);
        }

        [Test]
        public void NonTopLevelResourcesAreIgnoredInCalculatingTheDeploymentStatus()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var log = new InMemoryLog();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, reporter, log);

            var timer = new TestTimer(2);

            retriever.SetResponses(
                new List<Resource> { new TestResource("ReplicaSet", Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful, 
                    new Resource[]
                    {
                        new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Failed),
                        new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress)
                    })});
            
            var result = resourceStatusChecker.CheckStatusUntilCompletionOrTimeout(
                new ResourceIdentifier[]
                {
                    new ResourceIdentifier("ReplicaSet", "my-rs", "default")
                }, timer, null, new Options());

            result.Should().BeTrue();
            log.StandardError.Should().BeEmpty();
            log.StandardOut.Should().Contain(ResourceStatusChecker.MessageDeploymentSucceeded);
        }
        
        [Test]
        public void ShouldNotReturnSuccessIfSomeOfTheDefinedResourcesWereNotCreated()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var log = new InMemoryLog();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, reporter, log);

            var timer = new TestTimer(2);

            retriever.SetResponses(
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful) },
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful) }
            );
            
            var result = resourceStatusChecker.CheckStatusUntilCompletionOrTimeout(
                new ResourceIdentifier[]
                {
                    new ResourceIdentifier("Pod", "my-pod", "default"),
                    new ResourceIdentifier("Service", "my-service", "default")
                }, timer, null, new Options());

            result.Should().BeFalse();
            log.StandardError
                .Should().ContainSingle().Which
                .Should().Be(ResourceStatusChecker.MessageInProgressAtTheEndOfTimeout);
        }
    }

    public class TestRetriever : IResourceRetriever
    {
        private readonly List<List<Resource>> responses = new List<List<Resource>>();
        private int current;
        
        public IEnumerable<Resource> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, Kubectl kubectl, Options options)
        {
            return current >= responses.Count ? new List<Resource>() : responses[current++];
        }

        public void SetResponses(params List<Resource>[] responses)
        {
            this.responses.AddRange(responses);
        }
    }

    public class TestReporter : IResourceUpdateReporter
    {
        private readonly List<int> checkCounts = new List<int>();
        
        public void ReportUpdatedResources(IDictionary<string, Resource> originalStatuses, IDictionary<string, Resource> newStatuses, int checkCount)
        {
            checkCounts.Add(checkCount);
        }
        
        public List<int> CheckCounts() => checkCounts.ToList();
    }

    public class TestTimer : ITimer
    {
        private readonly int maxChecks;
        private int checks;
        
        public TestTimer(int maxChecks) => this.maxChecks = maxChecks;
        public void Start() { }
        public bool HasCompleted() => checks >= maxChecks;
        public void WaitForInterval() => checks++;
    }

    public sealed class TestResource : Resource
    {
        public TestResource(string kind, Kubernetes.ResourceStatus.Resources.ResourceStatus status, params Resource[] children)
        {
            Uid = Guid.NewGuid().ToString();
            Kind = kind;
            ResourceStatus = status;
            Children = children.ToList();
        }
        
        public TestResource(JObject json, Options options) : base(json, options)
        {
        }
    }
}
