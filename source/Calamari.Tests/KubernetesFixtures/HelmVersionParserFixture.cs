using Calamari.Util;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class HelmVersionParserFixture
    {
        [Test]
        public void ParseVersion_V2Version_V2()
        {
            HelmVersionParser.ParseVersion("Client: v2.16.1+gbbdfe5e").Should().Be(HelmVersion.V2);
        }
        
        [Test]
        public void ParseVersion_V3Version_V3()
        {
            HelmVersionParser.ParseVersion("v3.0.2+g19e47ee").Should().Be(HelmVersion.V3);
        }
        
        [Test]
        public void ParseVersion_OtherVersion_Null()
        {
            HelmVersionParser.ParseVersion("v4.0.2+g19e47ee").Should().BeNull();
        }

        [Test]
        public void ParseVersion_V3UppercaseVersion_Null()
        {
            HelmVersionParser.ParseVersion("V3.0.2+g19e47ee").Should().BeNull();
        }

        [Test]
        public void ParseVersion_V3WithoutPrefix_Null()
        {
            HelmVersionParser.ParseVersion("3.0.2+g19e47ee").Should().BeNull();
        }
        
        [TestCase("vzsd3242347ee", Description = "Has a v")]
        [TestCase("zsd3242347ee", Description = "No v")]
        [TestCase("v", Description = "Just v")]
        public void ParseVersion_Rubbish_Null(string version)
        {
            HelmVersionParser.ParseVersion(version).Should().BeNull();
        }
    }
}