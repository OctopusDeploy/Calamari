using Calamari.Kubernetes.ResourceStatus;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    [TestFixture]
    public class StabilizingTimerTests
    {
        [TestCase(DeploymentStatus.Failed, DeploymentStatus.InProgress)]
        [TestCase(DeploymentStatus.Failed, DeploymentStatus.Failed)]
        [TestCase(DeploymentStatus.Failed, DeploymentStatus.Succeeded)]
        [TestCase(DeploymentStatus.Succeeded, DeploymentStatus.InProgress)]
        [TestCase(DeploymentStatus.Succeeded, DeploymentStatus.Failed)]
        [TestCase(DeploymentStatus.Succeeded, DeploymentStatus.Succeeded)]
        public void ShouldNotContinueWhenDeploymentTimeoutIsReachedDuringStabilization(DeploymentStatus oldStatus, DeploymentStatus newStatus)
        {
            var deploymentTimer = new MockCountdownTimer();
            var stabilizationTimer = new MockCountdownTimer();
            var timer = new StabilizingTimer(deploymentTimer, stabilizationTimer);
            
            timer.Start();
            
            deploymentTimer.Complete();
            stabilizationTimer.Start();

            var shouldContinue = timer.ShouldContinue(
                oldStatus,
                newStatus);

            shouldContinue.Should().BeFalse();
        }
        
        [TestCase(DeploymentStatus.InProgress, DeploymentStatus.InProgress)]
        [TestCase(DeploymentStatus.InProgress, DeploymentStatus.Failed)]
        [TestCase(DeploymentStatus.InProgress, DeploymentStatus.Succeeded)]
        public void ShouldNotContinueWhenDeploymentTimeoutIsReachedWhenNotStabilizing(DeploymentStatus oldStatus, DeploymentStatus newStatus)
        {
            var deploymentTimer = new MockCountdownTimer();
            var stabilizationTimer = new MockCountdownTimer();
            var timer = new StabilizingTimer(deploymentTimer, stabilizationTimer);

            timer.Start();
            
            deploymentTimer.Complete();

            var shouldContinue = timer.ShouldContinue(
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
            var timer = new StabilizingTimer(deploymentTimer, stabilizationTimer);
            
            timer.Start();
            
            stabilizationTimer.Start();
            stabilizationTimer.Complete();
            
            var shouldContinue = timer.ShouldContinue(
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
            var timer = new StabilizingTimer(deploymentTimer, stabilizationTimer);
            
            timer.Start();
            
            stabilizationTimer.Start();

            var shouldContinue = timer.ShouldContinue(
                oldStatus,
                newStatus);

            shouldContinue.Should().BeTrue();
            timer.IsStabilizing().Should().Be(newStabilizationShouldStart);
        }

        [TestCase(DeploymentStatus.Succeeded, true)]
        [TestCase(DeploymentStatus.Failed, true)]
        [TestCase(DeploymentStatus.InProgress, false)]
        public void ShouldContinueWhenDeploymentTimeoutIsNotReachedAndNotStabilizing(DeploymentStatus newStatus, bool shouldStartStabilization)
        {
            var deploymentTimer = new MockCountdownTimer();
            var stabilizationTimer = new MockCountdownTimer();
            var timer = new StabilizingTimer(deploymentTimer, stabilizationTimer);

            timer.Start();
            
            var shouldContinue = timer.ShouldContinue(
                DeploymentStatus.InProgress,
                newStatus);

            shouldContinue.Should().BeTrue();
            timer.IsStabilizing().Should().Be(shouldStartStabilization);
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