using Calamari.Util;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class HelmVersionParserFixture
    {
        [Test]
        public void ParseMajorVersion_V2Version_2()
        {
            HelmVersionParser.ParseMajorVersion("Client: v2.16.1+gbbdfe5e").Should().Be(2);
        }

        [Test]
        public void ParseMajorVersion_V3Version_3()
        {
            HelmVersionParser.ParseMajorVersion("v3.0.2+g19e47ee").Should().Be(3);
        }

        [Test]
        public void ParseMajorVersion_V4Version_4()
        {
            HelmVersionParser.ParseMajorVersion("v4.0.2+g19e47ee").Should().Be(4);
        }

        [Test]
        public void ParseMajorVersion_V5Version_5()
        {
            HelmVersionParser.ParseMajorVersion("v5.0.0+g19e47ee").Should().Be(5);
        }

        [Test]
        public void ParseMajorVersion_V3UppercaseVersion_Null()
        {
            HelmVersionParser.ParseMajorVersion("V3.0.2+g19e47ee").Should().BeNull();
        }

        [Test]
        public void ParseMajorVersion_V3WithoutPrefix_Null()
        {
            HelmVersionParser.ParseMajorVersion("3.0.2+g19e47ee").Should().BeNull();
        }

        [TestCase("vzsd3242347ee", Description = "Has a v")]
        [TestCase("zsd3242347ee", Description = "No v")]
        [TestCase("v", Description = "Just v")]
        public void ParseMajorVersion_Rubbish_Null(string version)
        {
            HelmVersionParser.ParseMajorVersion(version).Should().BeNull();
        }
    }
}
