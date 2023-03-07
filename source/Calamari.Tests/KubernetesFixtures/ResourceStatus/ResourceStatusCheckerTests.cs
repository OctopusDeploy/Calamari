using Calamari.Kubernetes.ResourceStatus;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    [TestFixture]
    public class ResourceStatusCheckerTests
    {
        private readonly ResourceStatusChecker checker = new ResourceStatusChecker(null, null);
        
        [TestCase(true, DeploymentStatus.Failed, DeploymentStatus.InProgress)]
        [TestCase(true, DeploymentStatus.Failed, DeploymentStatus.Failed)]
        [TestCase(true, DeploymentStatus.Failed, DeploymentStatus.Succeeded)]
        [TestCase(true, DeploymentStatus.Succeeded, DeploymentStatus.InProgress)]
        [TestCase(true, DeploymentStatus.Succeeded, DeploymentStatus.Failed)]
        [TestCase(true, DeploymentStatus.Succeeded, DeploymentStatus.Succeeded)]
        [TestCase(false, DeploymentStatus.InProgress, DeploymentStatus.InProgress)]
        [TestCase(false, DeploymentStatus.InProgress, DeploymentStatus.Failed)]
        [TestCase(false, DeploymentStatus.InProgress, DeploymentStatus.Succeeded)]
        public void ShouldNotContinueWhenDeploymentTimeoutIsReached(bool isStabilizing, DeploymentStatus oldStatus, DeploymentStatus newStatus)
        {
            var deploymentTimer = new MockCountdownTimer();
            var stabilizationTimer = new MockCountdownTimer();

            deploymentTimer.Complete();
            if (isStabilizing)
            {
                stabilizationTimer.Start();
            }
            
            var shouldContinue = checker.ShouldContinue(
                deploymentTimer, 
                stabilizationTimer, 
                oldStatus,
                newStatus);

            shouldContinue.Should().BeFalse();
        }

        [TestCase(DeploymentStatus.Succeeded)]
        [TestCase(DeploymentStatus.Failed)]
        public void ShouldNotContinueWhenStabilizationTimeoutIsReachedAndTheStatusDidNotChange(DeploymentStatus status)
        {
            var deploymentTimer = new MockCountdownTimer();
            var stabilizationTimer = new MockCountdownTimer();
            
            stabilizationTimer.Start();
            stabilizationTimer.Complete();
            
            var shouldContinue = checker.ShouldContinue(
                deploymentTimer, 
                stabilizationTimer, 
                status,
                status);

            shouldContinue.Should().BeFalse();
        }

        [TestCase(DeploymentStatus.Failed, DeploymentStatus.Succeeded, true)]
        [TestCase(DeploymentStatus.Failed, DeploymentStatus.InProgress, false)]
        [TestCase(DeploymentStatus.Succeeded, DeploymentStatus.Failed, true)]
        [TestCase(DeploymentStatus.Succeeded, DeploymentStatus.InProgress, false)]
        public void ShouldContinueDuringStabilizationWhenStabilizationTimeoutHasNotBeenReachedAndTheStatusChanged(
            DeploymentStatus oldStatus, DeploymentStatus newStatus, bool newStabilizationShouldStart)
        {
            var deploymentTimer = new MockCountdownTimer();
            var stabilizationTimer = new MockCountdownTimer();
            
            stabilizationTimer.Start();

            var shouldContinue = checker.ShouldContinue(
                deploymentTimer, 
                stabilizationTimer, 
                oldStatus,
                newStatus);

            shouldContinue.Should().BeTrue();

            stabilizationTimer.HasStarted().Should().Be(newStabilizationShouldStart);
        }

        [TestCase(DeploymentStatus.Succeeded, true)]
        [TestCase(DeploymentStatus.Failed, true)]
        [TestCase(DeploymentStatus.InProgress, false)]
        public void ShouldContinueWhenDeploymentTimeoutIsNotReachedAndNotStabilizing(DeploymentStatus newStatus, bool shouldStartStabilization)
        {
            var deploymentTimer = new MockCountdownTimer();
            var stabilizationTimer = new MockCountdownTimer();

            var shouldContinue = checker.ShouldContinue(
                deploymentTimer, 
                stabilizationTimer, 
                DeploymentStatus.InProgress,
                newStatus);

            shouldContinue.Should().BeTrue();
            stabilizationTimer.HasStarted().Should().Be(shouldStartStabilization);
        }
    }

    public class MockCountdownTimer : ICountdownTimer
    {
        private bool started;
        private bool completed;

        public void Complete()
        {
            completed = true;
        }
        
        public void Start()
        {
            started = true;
        }

        public void Reset()
        {
            started = false;
            completed = false;
        }

        public bool HasStarted() => started;

        public bool HasCompleted() => completed;
    }
}