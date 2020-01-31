using Calamari.Util;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class HelmVersionParserFixture
    {
        [Test]
        public void ParseVersion_V2()
        {
            HelmVersionParser.ParseVersion("Client: v2.16.1+gbbdfe5e").Should().Be(HelmVersion.V2);
        }
        
        [Test]
        public void ParseVersion_V3()
        {
            HelmVersionParser.ParseVersion("v3.0.2+g19e47ee").Should().Be(HelmVersion.V3);
        }
    }
}