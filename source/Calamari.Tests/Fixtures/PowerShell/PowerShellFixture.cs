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
    public class PowerShellFixture : CalamariFixture
    {
        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        [TestCase("2", "PSVersion                      2.0")]
        [TestCase("2.0", "PSVersion                      2.0")]
        public void ShouldCustomizePowerShellVersionIfRequested(string customPowerShellVersion, string expectedLogMessage)
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set(SpecialVariables.Action.PowerShell.CustomPowerShellVersion, customPowerShellVersion);
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                // Let's just use the Hello.ps1 script for something simples
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", GetFixtureResouce("Scripts", "Hello.ps1"))
                    .Argument("variables", variablesFile));

                output.AssertZero();
                output.AssertOutput(expectedLogMessage);
                output.AssertOutput("Hello!");
            }
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldCallHello()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "Hello.ps1")));

            output.AssertZero();
            output.AssertOutput("Hello!");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
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
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldWriteServiceMessageForArtifacts()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "CanCreateArtifact.ps1")));

            output.AssertZero();
            output.AssertOutput("##octopus[createArtifact path='QzpcUGF0aFxGaWxlLnR4dA==' name='RmlsZS50eHQ=' length='MA==']");
            //output.ApproveOutput();
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldWriteVerboseMessageForArtifactsThatDoNotExist()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "WarningForMissingArtifact.ps1")));

            output.AssertZero();
            output.AssertOutput(@"There is no file at 'C:\NonExistantPath\NonExistantFile.txt' right now. Writing the service message just in case the file is available when the artifacts are collected at a later point in time.");
            output.AssertOutput("##octopus[createArtifact path='QzpcTm9uRXhpc3RhbnRQYXRoXE5vbkV4aXN0YW50RmlsZS50eHQ=' name='Tm9uRXhpc3RhbnRGaWxlLnR4dA==' length='MA==']");
            //output.ApproveOutput();
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldAllowDotSourcing()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "CanDotSource.ps1")));

            output.AssertZero();
            output.AssertOutput("Hello!");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
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
        [Category(TestEnvironment.CompatibleOS.Windows)]
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
        [Category(TestEnvironment.CompatibleOS.Windows)]
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
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldFailOnInvalid()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "Invalid.ps1")));

            output.AssertNonZero();
            output.AssertErrorOutput("A positional parameter cannot be found that accepts");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldFailOnInvalidSyntax()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "InvalidSyntax.ps1")));

            output.AssertNonZero();
            output.AssertErrorOutput("ParserError");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
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
        [Category(TestEnvironment.CompatibleOS.Windows)]
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
        [Description("Proves the changes to run-script are backwards compatible")]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldNotSubstituteVariablesByDefault()
        {
            // Use a temp file for the script because the file would have been mutated by other tests
            var scriptFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ps1");
            File.WriteAllText(scriptFile, "Write-Host \"Hello #{Octopus.Environment.Name}!\"");

            var variablesFile = Path.GetTempFileName();
            var variables = new VariableDictionary();
            variables.Set("Octopus.Environment.Name", "Production");
            variables.Save(variablesFile);

            using (new TemporaryFile(scriptFile))
            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", scriptFile)
                    .Argument("variables", variablesFile));

                output.AssertZero();
                output.AssertOutput("Hello #{Octopus.Environment.Name}!");
            }
        }

        [Test]
        [Description("Proves scripts can have variables substituted into them before running")]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldSubstituteVariablesIfRequested()
        {
            // Use a temp file for the script to avoid mutating the script file for other tests
            var scriptFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ps1");
            File.WriteAllText(scriptFile, "Write-Host \"Hello #{Octopus.Environment.Name}!\"");

            var variablesFile = Path.GetTempFileName();
            var variables = new VariableDictionary();
            variables.Set("Octopus.Environment.Name", "Production");
            variables.Save(variablesFile);

            using (new TemporaryFile(scriptFile))
            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", scriptFile)
                    .Argument("variables", variablesFile)
                    .Flag("substituteVariables"));

                output.AssertZero();
                output.AssertOutput("Substituting variables");
                output.AssertOutput("Hello Production!");
            }
        }

        [Test]
        [Description("Proves packaged scripts can have variables substituted into them before running")]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldSubstituteVariablesInPackagedScriptsIfRequested()
        {
            var variablesFile = Path.GetTempFileName();
            var variables = new VariableDictionary();
            variables.Set("Octopus.Environment.Name", "Production");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("package", GetFixtureResouce("Packages", "PackagedScript.1.0.0.zip"))
                    .Argument("script", "Deploy.ps1")
                    .Argument("variables", variablesFile)
                    .Flag("substituteVariables"));

                output.AssertZero();
                output.AssertOutput("Extracting package");
                output.AssertOutput("Substituting variables");
                output.AssertOutput("OctopusParameter: Production");
                output.AssertOutput("InlineVariable: Production");
                output.AssertOutput("VariableSubstitution: Production");
            }
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldPing()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "Ping.ps1")));

            output.AssertZero();
            output.AssertOutput("Pinging ");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldExecuteWhenPathContainsSingleQuote()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts\\Path With '", "PathWithSingleQuote.ps1")));

            output.AssertZero();
            output.AssertOutput("Hello from a path containing a '");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldExecuteWhenPathContainsDollar()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts\\Path With $", "PathWithDollar.ps1")));

            output.AssertZero();
            output.AssertOutput("Hello from a path containing a $");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        public void ThrowsExceptionOnNix()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "Hello.ps1")));

            output.AssertErrorOutput("Script type `ps1` unsupported on this platform");
        }
    }
}