using System.Text.RegularExpressions;
using Calamari.Kubernetes.Conventions;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class HelmValidationFixture
    {
        [TestCase("100")]
        [TestCase("100s")]
        [TestCase("100us")]
        [TestCase("100µs")]
        [TestCase("100m10s")]
        [TestCase("300ms")]
        [TestCase("-1.5h")]
        [TestCase("2h45m")]
        public void ValidateTimeouts(string timeout)
        {
            HelmUpgradeConvention.ValidateTimeout(timeout).Should().BeTrue();
        }
    }
}