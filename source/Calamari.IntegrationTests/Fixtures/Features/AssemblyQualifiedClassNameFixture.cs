using System.Reflection;
using Calamari.Features;
using NUnit.Framework;

namespace Calamari.IntegrationTests.Fixtures.Features
{
    [TestFixture]
    public class AssemblyQualifiedClassNameFixture
    {
        [Test]
        public void ParsesWhenJustClassNamePresent()
        {
            var c = new AssemblyQualifiedClassName("JustAClass");

            Assert.AreEqual("JustAClass", c.ClassName);
            Assert.IsNull(c.AssemblyName);
        }

        [Test]
        public void ParsesWhenFullyQualified()
        {
            var c = new AssemblyQualifiedClassName(this.GetType().GetTypeInfo().AssemblyQualifiedName);

            Assert.AreEqual("Calamari.Tests.Fixtures.Features.AssemblyQualifiedClassNameFixture", c.ClassName);
            Assert.AreEqual("Calamari.Tests", c.AssemblyName);
        }
    }
}
