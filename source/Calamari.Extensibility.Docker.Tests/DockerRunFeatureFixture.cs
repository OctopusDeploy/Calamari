using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Extensibility.FileSystem;
using NUnit.Framework;
using Calamari.Utilities;
using NSubstitute;

namespace Calamari.Extensibility.Docker.Tests
{
    [TestFixture]
    public class DockerRunFeatureFixture
    {
        readonly ICalamariFileSystem fileSystem = Substitute.For<ICalamariFileSystem>();

        [SetUp]
        public void SetUp()
        {
            string blah = Arg.Any<string>();
            fileSystem.CreateTemporaryFile(Arg.Any<string>(), out blah).Returns(x => {
                x[1] = "Banana." + x[0];
                return new MemoryStream();
            });
        }

        [Test]
        public void ScriptWithSupportedExceptionSucceeds()
        {
            var execution = Substitute.For<IScriptExecution>();
            execution.SupportedExtensions.Returns(new[] {"fake", "sh"});

            var feature = new DockerRunFeature(execution, fileSystem);

            feature.Install(new VariableDictionary());

            execution.Received()
                .InvokeFromFile(Arg.Is<string>(t => t.EndsWith("sh")), Arg.Is<string>(t => string.IsNullOrEmpty(t)));
        }

        [Test]
        public void NoScriptWithSupportedExceptionThrows()
        {
            var execution = Substitute.For<IScriptExecution>();
            execution.SupportedExtensions.Returns(new[] {"fake"});

            var fileSystem = Substitute.For<ICalamariFileSystem>();

            var feature = new DockerRunFeature(execution, fileSystem);

            Assert.Throws<Exception>(() => feature.Install(new VariableDictionary()),
                "Unable to find runnable script named `docker-run`");
        }
    }   
}
