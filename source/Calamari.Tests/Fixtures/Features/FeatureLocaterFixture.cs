using System;
using System.Collections.Generic;
using Calamari.Features;
using Calamari.Shared;
using Calamari.Shared.Convention;
using Calamari.Shared.Features;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Features
{
    [TestFixture]
    public class FeatureLocaterFixture
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
            assemblyLoader.Types.Returns(new [] {typeof(FeatureWithNonParameterlessConstructor)});
            using (var pl = new ProxyLog())
            {
                new FeatureLocator(assemblyLoader);
                StringAssert.Contains($"The feature handler `{typeof(FeatureWithNonParameterlessConstructor).FullName}` does not have a parameterless constructor so will be unable to be instantiated", pl.StdOut);
            }
        }

        [Test]
        public void WarningLoggedIfFailsToInstantiate()
        {
             assemblyLoader.Types.Returns(new [] {typeof(FeatureWithExceptionalConstructor) });
            using (var pl = new ProxyLog())
            {
                new FeatureLocator(assemblyLoader);
                StringAssert.Contains($"Error loading feature `{typeof(FeatureWithExceptionalConstructor).FullName}` so it will be ignored.", pl.StdOut);
            }
        }

        [Test]
        public void WarningLoggedIfTypeIsNotIFeature()
        {
            assemblyLoader.Types.Returns(new[] { typeof(HandlerForNonFeature) });
            using (var pl = new ProxyLog())
            {
                new FeatureLocator(assemblyLoader);
                StringAssert.Contains($"The the type described by feature handler `{typeof(HandlerForNonFeature).FullName}` does not impliment IFeature so will be ignored.", pl.StdOut);
            }
        }

        [Test]
        public void FindThrowsErrorWhenNotFound()
        {
            var locater = new FeatureLocator(assemblyLoader);
            Assert.Throws<InvalidOperationException>(() => locater.GetFeatureType("FakeName"), "Unable to find feature with name 'FakeName'");
        }


        public class FeatureWithNonParameterlessConstructor : IFeatureHandler
        {
            public FeatureWithNonParameterlessConstructor(string myValue){}

            public string Name { get; }
            public string Description { get; }
            public IEnumerable<string> ConventionDependencies { get; }
            public IFeature CreateFeature()
            {
                throw new NotImplementedException();
            }

            public Type Feature { get; } = typeof(IFeature);
        }

        public class FeatureWithExceptionalConstructor : IFeatureHandler
        {
            public FeatureWithExceptionalConstructor()
            {
                throw new Exception("Problem");
            }

            public string Name { get; }
            public string Description { get; }
            public IEnumerable<string> ConventionDependencies { get; }

            public IFeature CreateFeature()
            {
                throw new NotImplementedException();
            }

            public Type Feature { get; } = typeof(IFeature);
        }

        public class HandlerForNonFeature : IFeatureHandler
        {
            public string Name { get; }
            public string Description { get; }
            public IEnumerable<string> ConventionDependencies { get; }

            public IFeature CreateFeature()
            {
                throw new NotImplementedException();
            }

            public Type Feature { get; } = typeof(DateTime);
        }



    }
}
