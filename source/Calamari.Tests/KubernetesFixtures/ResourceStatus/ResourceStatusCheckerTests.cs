using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{

    [TestFixture]
    public class ResourceStatusCheckerTests
    {
        [Test]
        public async Task ShouldReportStatusesWithIncrementingCheckCount()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var kubectl = GetKubectl();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, reporter, new InMemoryLog());

            var timer = new TestTimer(5);

            for (var i = 0; i < 5; i++)
            {
                retriever.SetResponses(new List<Resource>{ new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress) });
            }

            await resourceStatusChecker.CheckStatusUntilCompletionOrTimeout(
                kubectl,
                new[]
                {
                    new ResourceIdentifier("Pod", "my-pod", "default")
                }, timer, new Options());

            reporter.CheckCounts().Should().BeEquivalentTo(new List<int> { 1, 2, 3, 4, 5 });
        }

        [Test]
        public async Task SuccessfulBeforeTimeout_ShouldReturnAsSuccessful()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
			var kubectl = GetKubectl();
            var log = new InMemoryLog();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, reporter, log);

            var timer = new TestTimer(2);

            retriever.SetResponses(
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful) }
            );

            var result = await resourceStatusChecker.CheckStatusUntilCompletionOrTimeout(
                kubectl,
                new[]
                {
                    new ResourceIdentifier("Pod", "my-pod", "default")
                }, timer, new Options());

            result.Should().BeTrue();
            log.StandardError.Should().BeEmpty();
            log.StandardOut.Should().Contain(ResourceStatusChecker.MessageDeploymentSucceeded);
        }

        [Test]
        public async Task FailureBeforeTimeout_ShouldReturnAsFailed()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
			var kubectl = GetKubectl();
            var log = new InMemoryLog();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, reporter, log);

            var timer = new TestTimer(2);

            retriever.SetResponses(
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Failed) }
            );

            var result = await resourceStatusChecker.CheckStatusUntilCompletionOrTimeout(
                kubectl,
                new[]
                {
                    new ResourceIdentifier("Pod", "my-pod", "default")
                }, timer, new Options());

            result.Should().BeFalse();
            log.StandardError
                .Should().ContainSingle().Which
                .Should().Be(ResourceStatusChecker.MessageDeploymentFailed);
        }

        [Test]
        public async Task DeploymentInProgressAtTheEndOfTimeout_ShouldReturnAsFailed()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var kubectl = GetKubectl();
            var log = new InMemoryLog();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, reporter, log);

            var timer = new TestTimer(2);

            retriever.SetResponses(
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress) },
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress) }
            );

            var result = await resourceStatusChecker.CheckStatusUntilCompletionOrTimeout(
                kubectl,
                new[]
                {
                    new ResourceIdentifier("Pod", "my-pod", "default")
                }, timer, new Options());

            result.Should().BeFalse();
            log.StandardError
                .Should().ContainSingle().Which
                .Should().Be(ResourceStatusChecker.MessageInProgressAtTheEndOfTimeout);
        }

        [Test]
        public async Task NonTopLevelResourcesAreIgnoredInCalculatingTheDeploymentStatus()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var kubectl = GetKubectl();
            var log = new InMemoryLog();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, reporter, log);

            var timer = new TestTimer(2);

            retriever.SetResponses(
                new List<Resource>
                {
                    new TestResource("ReplicaSet", Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful,
                        new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Failed),
                        new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress))
                });

            var result = await resourceStatusChecker.CheckStatusUntilCompletionOrTimeout(
                kubectl,
                new[]
                {
                    new ResourceIdentifier("ReplicaSet", "my-rs", "default")
                }, timer, new Options());

            result.Should().BeTrue();
            log.StandardError.Should().BeEmpty();
            log.StandardOut.Should().Contain(ResourceStatusChecker.MessageDeploymentSucceeded);
        }

        [Test]
        public async Task ShouldNotReturnSuccessIfSomeOfTheDefinedResourcesWereNotCreated()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var kubectl = GetKubectl();
			var log = new InMemoryLog();
            var resourceStatusChecker = new ResourceStatusChecker(retriever, reporter, log);

            var timer = new TestTimer(2);

            retriever.SetResponses(
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful) },
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful) }
            );

            var result = await resourceStatusChecker.CheckStatusUntilCompletionOrTimeout(
                kubectl,
                new[]
                {
                    new ResourceIdentifier("Pod", "my-pod", "default"),
                    new ResourceIdentifier("Service", "my-service", "default")
                }, timer, new Options());

            result.Should().BeFalse();
            log.StandardError
                .Should().ContainSingle().Which
                .Should().Be(ResourceStatusChecker.MessageInProgressAtTheEndOfTimeout);
        }

        private IKubectl GetKubectl()
        {
            var kubectl = Substitute.For<IKubectl>();
            kubectl.TrySetKubectl().Returns(true);
            return kubectl;
        }
    }

    public class TestRetriever : IResourceRetriever
    {
        private readonly List<List<Resource>> responses = new List<List<Resource>>();
        private int current;

        public IEnumerable<Resource> GetAllOwnedResources(
            IEnumerable<ResourceIdentifier> resourceIdentifiers,
            IKubectl kubectl,
            Options options)
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
        public void Restart() { }

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
