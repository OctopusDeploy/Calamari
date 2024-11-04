using Calamari.Util;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class GoDurationParserFixture
    {
        [TestCase("100")]
        [TestCase(" 100 ")]
        [TestCase("100s")]
        [TestCase("100us")]
        [TestCase("100µs")]
        [TestCase("100m10s")]
        [TestCase("300ms")]
        [TestCase("-1.5h")]
        [TestCase("2h45m")]
        public void ValidateTimeouts(string timeout)
        {
            GoDurationParser.ValidateDuration(timeout).Should().BeTrue();
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("100blah")]
        public void InvalidateTimeouts(string timeout)
        {
            GoDurationParser.ValidateDuration(timeout).Should().BeFalse();
        }
    }
}