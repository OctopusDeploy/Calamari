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
        public void ParseVersion_Rubbish_Null()
        {
            HelmVersionParser.ParseVersion("zsd3242347ee").Should().BeNull();
        }
    }
}