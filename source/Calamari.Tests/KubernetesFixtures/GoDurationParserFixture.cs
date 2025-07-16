using System;
using System.Linq;
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
        public void ValidateTimeouts(string duration)
        {
            GoDurationParser.ValidateDuration(duration).Should().BeTrue();
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("100blah")]
        public void InvalidateTimeouts(string duration)
        {
            GoDurationParser.ValidateDuration(duration).Should().BeFalse();
        }

        [TestCaseSource(nameof(ParseDurationTestData))]
        public void ParseDuration(string goDuration, TimeSpan parsedResult)
        {
            var result = GoDurationParser.ParseDuration(goDuration);

            result.Should().Be(parsedResult);
        }

        public static TestCaseData[] ParseDurationTestData => new[]
        {
            new TestCaseData("1h", TimeSpan.FromHours(1)),
            new TestCaseData("10m", TimeSpan.FromMinutes(10)),
            new TestCaseData("20s", TimeSpan.FromSeconds(20)),
            new TestCaseData("300ms", TimeSpan.FromMilliseconds(300)),
            new TestCaseData(" 100 ", TimeSpan.FromSeconds(100)),
            new TestCaseData("100", TimeSpan.FromSeconds(100)),
            new TestCaseData("300ms", TimeSpan.FromMilliseconds(300)),
            new TestCaseData("-1.5h", TimeSpan.FromHours(-1.5)),
            new TestCaseData("1h5m20s300ms",
                             new TimeSpan(0,
                                          1,
                                          5,
                                          20,
                                          300))
        };

        [TestCaseSource(nameof(TryParseDurationTestData))]
        public void TryParseDuration(string goDuration, bool expectedResult, TimeSpan expectedOutput)
        {
            var result = GoDurationParser.TryParseDuration(goDuration, out var timespan);
            
            result.Should().Be(expectedResult);
            if (result)
            {
                timespan.Should().Be(expectedOutput);
            }
        }

        public static TestCaseData[] TryParseDurationTestData =>
            ParseDurationTestData
                .Select(d => new TestCaseData(d.Arguments[0], true, d.Arguments[1]))
                .Concat(new[]
                {
                    new TestCaseData("", false, null),
                    new TestCaseData("   ", false, null),
                    new TestCaseData("100blah", false, null),
                })
                .ToArray();
    }
}