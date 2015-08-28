using System;
using System.IO;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.PowerShell
{
    [TestFixture]
    [Category(TestEnvironment.CompatableOS.Windows)]
    public class PowerShellFixture : CalamariFixture
    {
        [Test]
        public void ShouldCallHello()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "Hello.ps1")));

            output.AssertZero();
            output.AssertOutput("Hello!");
        }

        [Test]
        public void ShouldCaptureAllOutput()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "Output.ps1")));

            output.AssertNonZero();
            output.AssertOutput("Hello, write-host!");
            output.AssertOutput("Hello, write-output!");
            output.AssertOutput("Hello, write-verbose!");
            output.AssertOutput("Hello, write-warning!");
            output.AssertErrorOutput("Hello-Error!");
        }

        [Test]
        public void ShouldCreateArtifacts()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "CanCreateArtifact.ps1")));

            output.AssertZero();
            output.AssertOutput("##octopus[createArtifact path='QzpcUGF0aFxGaWxlLnR4dA==' name='RmlsZS50eHQ=' length='MA==']");
        }

        [Test]
        public void ShouldAllowDotSourcing()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "CanDotSource.ps1")));

            output.AssertZero();
            output.AssertOutput("Hello!");
        }

        [Test]
        public void ShouldSetVariables()
        {
            var variables = new VariableDictionary();

            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "CanSetVariable.ps1")), variables);

            output.AssertZero();
            output.AssertOutput("##octopus[setVariable name='VGVzdEE=' value='V29ybGQh']");
            Assert.AreEqual("World!", variables.Get("TestA"));
        }

        [Test]
        public void ShouldSetActionIndexedOutputVariables()
        {
            var variables = new VariableDictionary();
            variables.Set(SpecialVariables.Action.Name, "run-script");

            var output = Invoke(Calamari() 
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "CanSetVariable.ps1")), 
                variables);

            Assert.AreEqual("World!", variables.Get("Octopus.Action[run-script].Output.TestA"));
        }

        [Test]
        public void ShouldSetMachineIndexedOutputVariables()
        {
            var variables = new VariableDictionary();
            variables.Set(SpecialVariables.Action.Name, "run-script");
            variables.Set(SpecialVariables.Machine.Name, "App01");

            var output = Invoke(Calamari() 
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "CanSetVariable.ps1")), 
                variables);

            Assert.AreEqual("World!", variables.Get("Octopus.Action[run-script].Output[App01].TestA"));
        }

        [Test]
        public void ShouldFailOnInvalid()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "Invalid.ps1")));

            output.AssertNonZero();
            output.AssertErrorOutput("A positional parameter cannot be found that accepts");
        }

        [Test]
        public void ShouldFailOnInvalidSyntax()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "InvalidSyntax.ps1")));

            output.AssertNonZero();
            output.AssertErrorOutput("ParserError");
        }

        [Test]
        public void ShouldPrintVariables()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Variable1", "ABC");
            variables.Set("Variable2", "DEF");
            variables.Set("Variable3", "GHI");
            variables.Set("Foo_bar", "Hello");
            variables.Set("Host", "Never");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                   .Action("run-script")
                   .Argument("script", GetFixtureResouce("Scripts", "PrintVariables.ps1"))
                   .Argument("variables", variablesFile));

                output.AssertZero();
                output.AssertOutput("V1= ABC");
                output.AssertOutput("V2= DEF");
                output.AssertOutput("V3= GHI");
                output.AssertOutput("FooBar= Hello");     // Legacy - '_' used to be removed
                output.AssertOutput("Foo_Bar= Hello");    // Current - '_' is valid in PowerShell
            }
        }

        [Test]
        public void ShouldSupportModulesInVariables()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Octopus.Script.Module[Foo]", "function SayHello() { Write-Host \"Hello from module!\" }");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                   .Action("run-script")
                   .Argument("script", GetFixtureResouce("Scripts", "UseModule.ps1"))
                   .Argument("variables", variablesFile));

                output.AssertZero();
                output.AssertOutput("Hello from module!");
            }
        }

        [Test]
        public void ShouldPing()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "Ping.ps1")));

            output.AssertZero();
            output.AssertOutput("Pinging ");
        }

        [Test]
        public void ShouldExecuteWhenPathContainsSingleQuote()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts\\Path With '", "PathWithSingleQuote.ps1")));

            output.AssertZero();
            output.AssertOutput("Hello from a path containing a '");
        }
    }
}