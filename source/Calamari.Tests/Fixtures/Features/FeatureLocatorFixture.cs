using System;
using Calamari.Extensibility;
using Calamari.Extensibility.Features;
using Calamari.Features;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Features
{
    [TestFixture]
    public class FeatureLocatorFixture
    {
        private IAssemblyLoader assemblyLoader;

        [SetUp]
        public void Setup()
        {
            assemblyLoader = Substitute.For<IAssemblyLoader>();
        }

        [Test]
        public void WarningLoggedIfAttributeMissing()
        {
            assemblyLoader.Types.Returns(new[] {typeof(MissingAttribute)});
            using (var pl = new ProxyLog())
            {
                new FeatureLocator(assemblyLoader);
                StringAssert.Contains(
                    $"Feature `{typeof(MissingAttribute).FullName}` does not have a FeatureAttribute attribute so it will be ignored. This may be a fatal problem if it is required for this operation.",
                    pl.StdOut);
            }
        }

        [Test]
        public void ConventionReturnsWhenNameSearched()
        {
            assemblyLoader.Types.Returns(new[] {typeof(IncludesAttribute)});
            var locater = new FeatureLocator(assemblyLoader);
            var t = locater.Locate("MyName");
            Assert.AreEqual(typeof(IncludesAttribute), t);
        }

        [Test]
        public void ConventionReturnsNullWhenNotFound()
        {
            var locater = new FeatureLocator(assemblyLoader);
            Assert.Throws<InvalidOperationException>(() => locater.Locate("FakeName"), "Unable to find feature with name 'FakeName'");
        }

        public class MissingAttribute : IFeature
        {
            public void Install(IVariableDictionary variables)
            {
                throw new NotImplementedException();
            }
        }

        [Feature("MyName", "Here Is Info")]
        public class IncludesAttribute : IFeature
        {
            public void Install(IVariableDictionary variables)
            {
                throw new NotImplementedException();
            }
        }
    }
}