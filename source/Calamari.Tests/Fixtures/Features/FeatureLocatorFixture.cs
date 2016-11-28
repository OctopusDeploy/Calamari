using System;
using System.IO;
using Calamari.Extensibility;
using Calamari.Extensibility.Features;
using Calamari.Features;
using Calamari.Tests.Helpers;
using NUnit.Framework;


namespace Calamari.Tests.Fixtures.Features
{
    [TestFixture]
    public class FeatureLocatorFixture
    {
        private FeatureLocator locator;

        [SetUp]
        public void SetUp()
        {
            locator = new FeatureLocator(Path.Combine(TestEnvironment.CurrentWorkingDirectory, "../Calamari.Extensions", "Features"));
        }

        [Test]
        public void WarningLoggedIfAttributeMissing()
        {
            var typename = typeof(MissingAttribute).AssemblyQualifiedName;
            Assert.Throws<InvalidOperationException>(() => locator.Locate(typename),
                $"Feature `{typename}` does not have a FeatureAttribute attribute so is to be used in this operation.");
        }

        [Test]
        public void ConventionReturnsWhenNameSearched()
        {
            var t = locator.Locate(typeof(IncludesAttribute).AssemblyQualifiedName);
            Assert.AreEqual(typeof(IncludesAttribute), t.Feature);
        }

        [Test]
        public void FindsNameFromClassNotInProcess()
        {
            var t = locator.Locate("Calamari.Extensibility.RunScript.RunScriptInstallFeature, Calamari.Extensibility.RunScript");
            Assert.IsNotNull(t.Feature);
        }

        [Test]
        public void LocateThrowsWhenFeatureNotFound()
        {
            Assert.Throws<InvalidOperationException>(() => locator.Locate("FakeName"), "Unable to determine feature from name `FakeName`");
        }

        [Test]
        public void LocateThrowsWhenFeatureHasDodgyModule()
        {
            Assert.Throws<InvalidOperationException>(() => locator.Locate(typeof(FeatureWithDodgyModule).AssemblyQualifiedName), 
                $"Module `{typeof(ModuleWithNoDefaultConstructor).FullName}` does not have a default parameterless constructor.");
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


        [Feature("MyName", "Here Is Info", Module = typeof(ModuleWithNoDefaultConstructor))]
        public class FeatureWithDodgyModule : IFeature
        {
            public void Install(IVariableDictionary variables)
            {
                throw new NotImplementedException();
            }
        }

        public class ModuleWithNoDefaultConstructor :IModule
        {
            public ModuleWithNoDefaultConstructor(int value){
                
            }

            public void Register(ICalamariContainer container)
            {
                throw new NotImplementedException();
            }
        }
    }
}