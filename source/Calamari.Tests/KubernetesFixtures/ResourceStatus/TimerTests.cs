using System;
using Calamari.Kubernetes.ResourceStatus;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures.ResourceStatus
{
    [TestFixture]
    public class TimerTests
    {
        [Test]
        public void ZeroDurationTimer_ShouldNotCompleteBeforeItIsStarted()
        {
            var timer = new Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1));
            timer.HasCompleted().Should().BeFalse();
        }
    }
}
