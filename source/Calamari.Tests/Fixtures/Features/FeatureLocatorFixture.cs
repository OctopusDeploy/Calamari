using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Calamari.Extensibility;
using Calamari.Extensibility.Features;
using Calamari.Extensibility.FileSystem;
using Calamari.Features;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Features
{

    [TestFixture]
    public class FeatureLocatorFixture
    {
        private static readonly string RunScriptAssembly = "Calamari.Extensibility.FakeFeatures";

        private static readonly string RunScriptFeatureName =
            $"Calamari.Extensibility.FakeFeatures.HelloWorldFeature, {RunScriptAssembly}";

        private readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        private PackageStore packageStore;
        private string RandomTestDirectory = "";

#if NET40
        private static string Framework = "net40";
#else
        private static string Framework = "netstandard1.6";
#endif

        [SetUp]
        public void SetUp()
        {
            RandomTestDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Environment.SetEnvironmentVariable("TentacleHome", Path.GetTempPath());
            packageStore = new PackageStore(new GenericPackageExtractor());
        }

        FeatureLocator BuildLocator(string customDirectory = null)
        {
            return new FeatureLocator(new GenericPackageExtractor(), packageStore, fileSystem, customDirectory);
        }

        
        [Test]
        [Ignore("No longer checking attribute")]
        public void WarningLoggedIfAttributeMissing()
        {
            var locator = BuildLocator();
            var typename = typeof(MissingAttribute).AssemblyQualifiedName;
            Assert.Throws<InvalidOperationException>(() => locator.Locate(typename),
                $"Feature `{typename}` does not have a FeatureAttribute attribute so is to be used in this operation.");
        }

        [Test]
        public void ConventionReturnsWhenNameSearched()
        {
            var locator = BuildLocator();
            var t = locator.Locate(typeof(IncludesAttribute).AssemblyQualifiedName);
            Assert.AreEqual(typeof(IncludesAttribute), t.Feature);
        }
    
        [Test]
        public void LocateReturnsNullWhenFeatureNotFoundInValidAssembly()
        {
            var myAssembly = RandomAssembly();
            MockExtensionBuilder.Build(AssemblyPath(RandomTestDirectory, myAssembly), myAssembly);


            var t = BuildLocator(RandomTestDirectory)
                .Locate($"Calamari.Extensibility.Elsewhere.MissingFeature, {myAssembly.AssemblyName}, Version={myAssembly.Version}");
            Assert.IsNull(t);
        }
        
        [Test]
        public void LocateReturnsNullWhenAssemblyNotFound()
        {
            var locator = BuildLocator();
            var type = locator.Locate("MyClass, MyAssembly");
            Assert.IsNull(type);
        }
        
        [Test]
        public void LocateThrowsWhenFeatureTypeNotParseable()
        {
            var locator = BuildLocator();
            Assert.Throws<InvalidOperationException>(() => locator.Locate("FakeName"), "Unable to determine feature from name `FakeName`");
        }

        [Test]
        public void LocateThrowsWhenFeatureHasDodgyModule()
        {
            var locator = BuildLocator();
            Assert.Throws<InvalidOperationException>(() => locator.Locate(typeof(FeatureWithDodgyModule).AssemblyQualifiedName), 
                $"Module `{typeof(ModuleWithNoDefaultConstructor).FullName}` does not have a default parameterless constructor.");
        }

        [Test]
        public void FindsFeatureFromBuiltInDirectory()
        {
            var myAssembly = RandomAssembly();

            MockExtensionBuilder.Build(AssemblyPath(RandomTestDirectory, myAssembly), myAssembly);

            var locator = BuildLocator();
            locator.BuiltInExtensionsPath = RandomTestDirectory;
            var type = locator.Locate(myAssembly.ToString());
            Assert.AreEqual(myAssembly.ClassName, type.Feature.GetTypeInfo().FullName);
        }

        [Test]
        public void FindsFeatureFromCustomDirectory()
        {
            var myAssembly = RandomAssembly();            
            MockExtensionBuilder.Build(AssemblyPath(RandomTestDirectory, myAssembly), myAssembly);

            var locator = BuildLocator(RandomTestDirectory);

            var type = locator.Locate(myAssembly.ToString());
            Assert.AreEqual(myAssembly.ClassName, type.Feature.GetTypeInfo().FullName);
        }

        [Test]
        public void FindsFeatureWithCorrectVersionCustomDirectory()
        {
            var randomBits = Guid.NewGuid().ToString("N").Substring(0, 6);

            var myAssembly1 = RandomAssembly("1.0.0.0", randomBits);
            MockExtensionBuilder.Build(AssemblyPath(RandomTestDirectory, myAssembly1), myAssembly1);

            var myAssembly2 = RandomAssembly("2.0.0.0", randomBits);
            MockExtensionBuilder.Build(AssemblyPath(RandomTestDirectory, myAssembly2), myAssembly2);

            var myAssembly3 = RandomAssembly("3.0.0.0", randomBits);
            MockExtensionBuilder.Build(AssemblyPath(RandomTestDirectory, myAssembly3), myAssembly1);


            var locator = BuildLocator(RandomTestDirectory);

            var type = locator.Locate(myAssembly2.ToString());
            Assert.AreEqual(myAssembly2.ClassName, type.Feature.GetTypeInfo().FullName);
            Assert.AreEqual(new Version("2.0.0.0"), type.Feature.GetTypeInfo().Assembly.GetName().Version);
        }

        [Test]
        public void FindsFeatureFromTentacleHome()
        {
            var myAssembly = RandomAssembly();
            var dir = Path.Combine(RandomTestDirectory, "Extensions", myAssembly.AssemblyName, myAssembly.Version.ToString(), Framework);
            MockExtensionBuilder.Build(dir, myAssembly);

            Environment.SetEnvironmentVariable("TentacleHome", RandomTestDirectory);

            var locator = BuildLocator();

            var type = locator.Locate(myAssembly.ToString());
            Assert.AreEqual(myAssembly.ClassName, type.Feature.GetTypeInfo().FullName);
        }
        
        [Test]
        public void FindsFeatureInPacakge()
        {
            Environment.SetEnvironmentVariable("TentacleHome", RandomTestDirectory);

            packageStore.PackagesDirectory = TestEnvironment.GetTestPath("Fixtures", "Features", "Packages");

            var locator = BuildLocator();
            
            var t = locator.Locate(RunScriptFeatureName +", Version=1.0.0.0");
            Assert.IsNotNull(t.Feature);
            Assert.AreEqual(new Version(1, 0, 0, 0), t.Feature.GetTypeInfo().Assembly.GetName().Version);
        }

        AssemblyQualifiedClassName RandomAssembly(string version = "1.0.0.0", string randomBits = null)
        {
            randomBits = (randomBits ?? Guid.NewGuid().ToString("N").Substring(0, 6));
            return new AssemblyQualifiedClassName($"Calamari.Extensibility.Fake1_{randomBits}.FakeFeature, Calamari.Extensibility.Fake1_{randomBits}, Version={version}");
        }

        string AssemblyPath(string root, AssemblyQualifiedClassName myAssembly)
        {
            return Path.Combine(root, myAssembly.AssemblyName, myAssembly.Version.ToString(), Framework);
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
        
        public class ModuleWithNoDefaultConstructor : IModule
        {
            public ModuleWithNoDefaultConstructor(int value)
            {

            }

            public void Register(ICalamariContainer container)
            {
                throw new NotImplementedException();
            }
        }
    }
}