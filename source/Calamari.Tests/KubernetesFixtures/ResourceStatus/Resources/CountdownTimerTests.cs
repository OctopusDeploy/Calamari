using System;
using Calamari.Kubernetes.ResourceStatus;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus.Resources
{
    [TestFixture]
    public class CountdownTimerTests
    {
        [Test]
        public void ZeroDurationCountdownTimer_ShouldNotCompleteBeforeItIsStarted()
        {
            var timer = new CountdownTimer(TimeSpan.Zero);
            timer.HasCompleted().Should().BeFalse();
        }
    }
}
