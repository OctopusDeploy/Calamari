using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using ITimer = Calamari.Kubernetes.ResourceStatus.ITimer;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{

    [TestFixture]
    public class RunningResourceStatusCheckTests
    {
        [Test]
        public async Task ShouldReportStatusesWithIncrementingCheckCount()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var kubectl = Substitute.For<IKubectl>();
            var statusCheckTaskFactory = GetStatusCheckTaskFactory(retriever, reporter, kubectl, maxChecks: 5);
            var log = new InMemoryLog();

            for (var i = 0; i < 5; i++)
            {
                retriever.SetResponses(new List<Resource>{ new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress) });
            }

            var resourceStatusChecker = new RunningResourceStatusCheck(statusCheckTaskFactory, log, new TimeSpan(), new Options(), new[]
            {
                new ResourceIdentifier("", "v1", "Pod", "my-pod", "default")
            });

            await resourceStatusChecker.WaitForCompletionOrTimeout(CancellationToken.None);

            reporter.CheckCounts().Should().BeEquivalentTo(new List<int> { 1, 2, 3, 4, 5 });
        }

        [Test]
        public async Task SuccessfulBeforeTimeout_ShouldReturnAsSuccessful()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var kubectl = Substitute.For<IKubectl>();
            var statusCheckTaskFactory = GetStatusCheckTaskFactory(retriever, reporter, kubectl, maxChecks: 5);
            var log = new InMemoryLog();

            retriever.SetResponses(
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful) }
            );

            var resourceStatusChecker = new RunningResourceStatusCheck(statusCheckTaskFactory, log, new TimeSpan(), new Options(), new[]
            {
                new ResourceIdentifier("", "v1", "Pod", "my-pod", "default")
            });

            var result = await resourceStatusChecker.WaitForCompletionOrTimeout(CancellationToken.None);

            result.Should().BeTrue();
            log.StandardError.Should().BeEmpty();
            log.StandardOut.Should().Contain(RunningResourceStatusCheck.MessageDeploymentSucceeded);
        }

        [Test]
        public async Task FailureBeforeTimeout_ShouldReturnAsFailed()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var kubectl = Substitute.For<IKubectl>();
            var statusCheckTaskFactory = GetStatusCheckTaskFactory(retriever, reporter, kubectl, maxChecks: 5);
            var log = new InMemoryLog();

            retriever.SetResponses(
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Failed) }
            );

            var resourceStatusChecker = new RunningResourceStatusCheck(statusCheckTaskFactory, log, new TimeSpan(), new Options(), new[]
            {
                new ResourceIdentifier("", "v1", "Pod", "my-pod", "default")
            });

            var result = await resourceStatusChecker.WaitForCompletionOrTimeout(CancellationToken.None);

            result.Should().BeFalse();
            log.StandardError
                .Should().ContainSingle().Which
                .Should().Be(RunningResourceStatusCheck.MessageDeploymentFailed);
        }

        [Test]
        public async Task DeploymentInProgressAtTheEndOfTimeout_ShouldReturnAsFailed()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var kubectl = Substitute.For<IKubectl>();
            var statusCheckTaskFactory = GetStatusCheckTaskFactory(retriever, reporter, kubectl, maxChecks: 5);
            var log = new InMemoryLog();

            retriever.SetResponses(
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress) },
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress) }
            );

            var resourceStatusChecker = new RunningResourceStatusCheck(statusCheckTaskFactory, log, new TimeSpan(), new Options(), new[]
            {
                new ResourceIdentifier("", "v1", "Pod", "my-pod", "default")
            });

            var result = await resourceStatusChecker.WaitForCompletionOrTimeout(CancellationToken.None);

            result.Should().BeFalse();
            log.StandardError
                .Should().ContainSingle().Which
                .Should().Be(RunningResourceStatusCheck.MessageInProgressAtTheEndOfTimeout);
        }

        [Test]
        public async Task NonTopLevelResourcesAreIgnoredInCalculatingTheDeploymentStatus()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var kubectl = Substitute.For<IKubectl>();
            var statusCheckTaskFactory = GetStatusCheckTaskFactory(retriever, reporter, kubectl, maxChecks: 5);
            var log = new InMemoryLog();

            retriever.SetResponses(
                new List<Resource>
                {
                    new TestResource("ReplicaSet", Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful,
                        new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Failed),
                        new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.InProgress))
                });

            var resourceStatusChecker = new RunningResourceStatusCheck(statusCheckTaskFactory, log, new TimeSpan(), new Options(), new[]
            {
                new ResourceIdentifier("", "v1", "ReplicaSet", "my-rs", "default")
            });

            var result = await resourceStatusChecker.WaitForCompletionOrTimeout(CancellationToken.None);

            result.Should().BeTrue();
            log.StandardError.Should().BeEmpty();
            log.StandardOut.Should().Contain(RunningResourceStatusCheck.MessageDeploymentSucceeded);
        }

        [Test]
        public async Task ShouldNotReturnSuccessIfSomeOfTheDefinedResourcesWereNotCreated()
        {
            var retriever = new TestRetriever();
            var reporter = new TestReporter();
            var kubectl = Substitute.For<IKubectl>();
            var statusCheckTaskFactory = GetStatusCheckTaskFactory(retriever, reporter, kubectl, maxChecks: 5);
            var log = new InMemoryLog();

            retriever.SetResponses(
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful) },
                new List<Resource> { new TestResource("Pod", Kubernetes.ResourceStatus.Resources.ResourceStatus.Successful) }
            );

            var resourceStatusChecker = new RunningResourceStatusCheck(statusCheckTaskFactory, log, new TimeSpan(), new Options(), new[]
            {
                new ResourceIdentifier("", "v1", "Pod", "my-pod", "default"),
                new ResourceIdentifier("", "v1", "Service", "my-service", "default")
            });

            var result = await resourceStatusChecker.WaitForCompletionOrTimeout(CancellationToken.None);

            result.Should().BeFalse();
            log.StandardError
                .Should().ContainSingle().Which
                .Should().Be(RunningResourceStatusCheck.MessageInProgressAtTheEndOfTimeout);
        }

        private Func<ResourceStatusCheckTask> GetStatusCheckTaskFactory(IResourceRetriever retriever,
            IResourceUpdateReporter reporter, IKubectl kubectl, int maxChecks)
        {
            return () => new ResourceStatusCheckTask(retriever, reporter, kubectl, (_, __) => new TestTimer(maxChecks));
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

        public bool HasCompleted() => checks >= maxChecks;
        public async Task WaitForInterval()
        {
            await Task.CompletedTask;
            checks++;
        }
    }

    public sealed class TestResource : Resource
    {
        public TestResource(string kind, Kubernetes.ResourceStatus.Resources.ResourceStatus status, params Resource[] children)
        {
            Uid = Guid.NewGuid().ToString();
            Group = "";
            Version = "v1";
            Kind = kind;
            ResourceStatus = status;
            Children = children.ToList();
        }

        public TestResource(JObject json, Options options) : base(json, options)
        {
        }
    }
}
