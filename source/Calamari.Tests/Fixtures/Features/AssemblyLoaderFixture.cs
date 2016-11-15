using System;
using System.IO;
using System.Linq;
using Calamari.Features;
using Calamari.Features.Conventions;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Features
{
    [TestFixture]
    public class AssemblyLoaderFixture :CalamariFixture
    {

        [Test]
        public void ThrowTryingToLoadMissingDirectory()
        {
            var locator = new AssemblyLoader();
            var extensionsDirectory = Path.Combine(TestEnvironment.CurrentWorkingDirectory, "FakeDirectory");
            Assert.Throws<InvalidOperationException>(() => locator.RegistryAssembly(extensionsDirectory));
        }
        

        [Test]
        public void FindsHandlerFromAssembly()
        {
            var locator = new AssemblyLoader();
            var extensionsDirectory = Path.Combine(TestEnvironment.CurrentWorkingDirectory, "Extensions");
            locator.RegistryAssembly(extensionsDirectory);

            
            CollectionAssert.IsNotEmpty(locator.Types);
            //var handler = locator.Find("RunScript");
            //Assert.IsInstanceOf<RunScriptMetaData>(handler);
        }

        [Test]
        public void LogsWarningIfNoParameterlessHandlerConstructor() { }

        [Test]
        public void LogsWarningIfHandlerConstructorFails() { }
    }
}