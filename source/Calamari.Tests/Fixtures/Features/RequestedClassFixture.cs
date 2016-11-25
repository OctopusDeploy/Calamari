using Calamari.Features;
using NUnit.Framework;
using System.Reflection;

namespace Calamari.Tests.Fixtures.Features
{
    [TestFixture]
    public class RequestedClassFixture
    {
        [Test]
        public void ParsesWhenJustClassNamePresent()
        {
            var c = RequestedClass.ParseFromAssemblyQualifiedName("JustAClass");

            Assert.AreEqual("JustAClass", c.ClassName);
            Assert.IsNull(c.AssemblyName);
        }

        [Test]
        public void ParsesWhenFullyQualified()
        {
            var c = RequestedClass.ParseFromAssemblyQualifiedName(this.GetType().GetTypeInfo().AssemblyQualifiedName);

            Assert.AreEqual("Calamari.Tests.Fixtures.Features.RequestedClassFixture", c.ClassName);
            Assert.AreEqual("Calamari.Tests", c.AssemblyName);
        }
    }
}
