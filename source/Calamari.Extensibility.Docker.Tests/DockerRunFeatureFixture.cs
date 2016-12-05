using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Extensibility.FileSystem;
using Calamari.Integration.FileSystem;
using NUnit.Framework;
using Calamari.Utilities;
using Calamari.Utilities.FileSystem;
using NSubstitute;

namespace Calamari.Extensibility.Docker.Tests
{
    [TestFixture]
    public class DockerRunFeatureFixture
    {
        [Test]
        public void ScriptWithSupportedExceptionSucceeds()
        {
            var execution = Substitute.For<IScriptExecution>();
            execution.SupportedExtensions.Returns(new[] {"fake", "sh"});

            var feature = new DockerRunFeature(execution, CalamariPhysicalFileSystem.GetPhysicalFileSystem());

            feature.Install(new VariableDictionary());

            execution.Received()
                .InvokeFromFile(Arg.Is<string>(t => t.EndsWith("sh")), Arg.Is<string>(t => string.IsNullOrEmpty(t)));
        }

        [Test]
        public void NoScriptWithSupportedExceptionThrows()
        {
            var execution = Substitute.For<IScriptExecution>();
            execution.SupportedExtensions.Returns(new[] { "fake" });

            var feature = new DockerRunFeature(execution, CalamariPhysicalFileSystem.GetPhysicalFileSystem());

            Assert.Throws<Exception>(() => feature.Install(new VariableDictionary()), 
                "Unable to find runnable script named `docker-run`");
        }

        [Test]
        public void ScripxxxtWithSupportedExceptionSucceeds()
        {
            var execution = Substitute.For<IScriptExecution>();
            execution.SupportedExtensions.Returns(new[] { "fake", "sh" });

            var feature = new DockerRunFeature(execution, CalamariPhysicalFileSystem.GetPhysicalFileSystem());

            feature.Install(new VariableDictionary());
//
//            var fileName = execution.ReceivedCalls().Last().GetArguments().First().ToString();
//            var test = File.ReadAllText(fileName);
//            
        }
    }   
}
