using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Calamari.Features;
using Calamari.Shared;
using Calamari.Shared.Convention;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Features
{
    [TestFixture]
    public class ConventionLocatorFixture
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
                new ConventionLocator(assemblyLoader);
                StringAssert.Contains(
                    $"Convention `{typeof(MissingAttribute).FullName}` does not have a ConventionMetadata attribute so it will be ignored",
                    pl.StdOut);
            }
        }

        [Test]
        public void ConventionReturnsWhenNameSearched()
        {
            assemblyLoader.Types.Returns(new[] {typeof(IncludesAttribute)});
            var locater = new ConventionLocator(assemblyLoader);
            var t = locater.Locate("MyName");
            Assert.AreEqual(typeof(IncludesAttribute), t);
        }

        [Test]
        public void ConventionReturnsNullWhenNotFound()
        {
            var locater = new ConventionLocator(assemblyLoader);
            Assert.IsNull(locater.Locate("FakeName"));
        }

        public class MissingAttribute : IInstallConvention
        {
            public void Install(IVariableDictionary variables)
            {
                throw new NotImplementedException();
            }
        }

        [ConventionMetadata("MyName", "Here Is Info")]
        public class IncludesAttribute : IInstallConvention
        {
            public void Install(IVariableDictionary variables)
            {
                throw new NotImplementedException();
            }
        }
    }
}