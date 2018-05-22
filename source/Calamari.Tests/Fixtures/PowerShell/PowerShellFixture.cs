using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Assent;
using Assent.Namers;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using Newtonsoft.Json;
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

                output.AssertSuccess();
                output.AssertOutput(expectedLogMessage);
                output.AssertOutput("Hello!");
            }
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        [TestCase("true", true)]
        [TestCase("false", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void ShouldCallWithNoProfileWhenVariableSet(string executeWithoutProfile, bool calledWithNoProfile)
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            if(executeWithoutProfile != null)
                variables.Set(SpecialVariables.Action.PowerShell.ExecuteWithoutProfile, executeWithoutProfile);
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", GetFixtureResouce("Scripts", "Profile.ps1"))
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                var allOutput = string.Join(Environment.NewLine, output.CapturedOutput.Infos);
                // Need to check for "-NoProfile -NoLogo" not just "-NoProfile" because when
                // run via Cake we end up with the outer Powershell call included in the
                // output too, which has a -NoProfile flag.
                Assert.That(allOutput.Contains("-NoProfile -NoLo") == calledWithNoProfile);
            }
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldNotCallWithNoProfileWhenVariableNotSet()
        {
            var (output, _) = RunScript("Profile.ps1", new Dictionary<string, string>()
                {[SpecialVariables.Action.PowerShell.ExecuteWithoutProfile] = "true"});

            output.AssertSuccess();
            output.AssertOutput("-NoProfile");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldCallHello()
        {
            var (output, _) = RunScript("Hello.ps1");

            output.AssertSuccess();
            output.AssertOutput("Hello!");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldLogWarningIfScriptArgumentUsed()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "Hello.ps1")));

            output.AssertSuccess();
            output.AssertOutput("##octopus[stdout-warning]\r\nThe `--script` parameter is depricated.");
            output.AssertOutput("Hello!");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldRetrieveCustomReturnValue()
        {
            var (output, _) = RunScript("Exit2.ps1");

            output.AssertFailure(2);
            output.AssertOutput("Hello!");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Name", "NameToEncrypt");
            variables.SaveEncrypted("5XETGOgqYR2bRhlfhDruEg==", variablesFile);

            var (output, _) = RunScript("HelloWithVariable.ps1", new Dictionary<string, string>()
                {["Name"] = "NameToEncrypt"}, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            output.AssertSuccess();
            output.AssertOutput("Hello NameToEncrypt");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldCallHelloWithAdditionalBase64Variable()
        {
            var variables = new Dictionary<string, string>() { ["Octopus.Action[PreviousStep].Output.FirstName"] = "Steve" } ;
            var serialized = JsonConvert.SerializeObject(variables);
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(serialized));

            var (output, _) = RunScript("OuputVariableFromPrevious.ps1", null, new Dictionary<string, string>()
            {
                ["base64Variables"] = encoded
            });

            output.AssertSuccess();
            output.AssertOutput("Hello Steve");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunScript("Parameters.ps1", additionalParameters: new Dictionary<string, string>()
            {
                ["scriptParameters"] = "-Parameter0 \"Para meter0\" -Parameter1 'Para meter1'"
            });
            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Para meter1");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldCaptureAllOutput()
        {
            var (output, _) = RunScript("Output.ps1");
            output.AssertFailure();
            output.AssertOutput("Hello, write-host!");
            output.AssertOutput("Hello, write-output!");
            output.AssertOutput("Hello, write-verbose!");
            output.AssertOutput("Hello, write-warning!");
            output.AssertErrorOutput("Hello-Error!");
            output.AssertNoOutput("This warning should not appear in logs!");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldWriteServiceMessageForArtifacts()
        {
            var (output, _) = RunScript("CanCreateArtifact.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact path='QzpcUGF0aFxGaWxlLnR4dA==' name='RmlsZS50eHQ=' length='MA==']");
            //  this.Assent(output.CapturedOutput.ToApprovalString(), new Configuration().UsingNamer(new SubdirectoryNamer("Approved")));
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldWriteVerboseMessageForArtifactsThatDoNotExist()
        {
            var (output, _) = RunScript("WarningForMissingArtifact.ps1");
            output.AssertSuccess();
            output.AssertOutput(@"There is no file at 'C:\NonExistantPath\NonExistantFile.txt' right now. Writing the service message just in case the file is available when the artifacts are collected at a later point in time.");
            output.AssertOutput("##octopus[createArtifact path='QzpcTm9uRXhpc3RhbnRQYXRoXE5vbkV4aXN0YW50RmlsZS50eHQ=' name='Tm9uRXhpc3RhbnRGaWxlLnR4dA==' length='MA==']");
            // this.Assent(output.CapturedOutput.ToApprovalString(), new Configuration().UsingNamer(new SubdirectoryNamer("Approved")));
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldAllowDotSourcing()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "CanDotSource.ps1")));

            output.AssertSuccess();
            output.AssertOutput("Hello!");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldSetVariables()
        {
            var (output, variables) = RunScript("CanSetVariable.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='VGVzdEE=' value='V29ybGQh']");
            Assert.AreEqual("World!", variables.Get("TestA"));
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldSetSensitiveVariables()
        {
            var (output, variables) = RunScript("CanSetVariable.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='U2VjcmV0U3F1aXJyZWw=' value='WCBtYXJrcyB0aGUgc3BvdA==' sensitive='VHJ1ZQ==']");
            Assert.AreEqual("X marks the spot", variables.Get("SecretSquirrel"));
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldSetActionIndexedOutputVariables()
        {
            var (_, variables) = RunScript("CanSetVariable.ps1", new Dictionary<string, string>
            {
                [SpecialVariables.Action.Name] = "run-script"
            });
            Assert.AreEqual("World!", variables.Get("Octopus.Action[run-script].Output.TestA"));
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldSetMachineIndexedOutputVariables()
        {
            var (_, variables) = RunScript("CanSetVariable.ps1", new Dictionary<string, string>
            {
                [SpecialVariables.Action.Name] = "run-script",
                [SpecialVariables.Machine.Name] = "App01"
            });

            Assert.AreEqual("World!", variables.Get("Octopus.Action[run-script].Output[App01].TestA"));
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldFailOnInvalid()
        {
            var (output, _) = RunScript("Invalid.ps1");
            output.AssertFailure();
            output.AssertErrorOutput("A positional parameter cannot be found that accepts");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldFailOnInvalidSyntax()
        {
            var (output, _) = RunScript("InvalidSyntax.ps1");
            output.AssertFailure();
            output.AssertErrorOutput("ParserError");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldPrintVariables()
        {
            var (output, _) = RunScript("PrintVariables.ps1", new Dictionary<string, string>
            {
                ["Variable1"] = "ABC",
                ["Variable2"] = "DEF",
                ["Variable3"] = "GHI",
                ["Foo_bar"] = "Hello",
                ["Host"] = "Never",
            });


            output.AssertSuccess();
            output.AssertOutput("V1= ABC");
            output.AssertOutput("V2= DEF");
            output.AssertOutput("V3= GHI");
            output.AssertOutput("FooBar= Hello"); // Legacy - '_' used to be removed
            output.AssertOutput("Foo_Bar= Hello"); // Current - '_' is valid in PowerShell

        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldSupportModulesInVariables()
        {
            var (output, _) = RunScript("UseModule.ps1", new Dictionary<string, string>
            {
                ["Octopus.Script.Module[Foo]"] = "function SayHello() { Write-Host \"Hello from module!\" }",
                ["Variable2"] = "DEF",
                ["Variable3"] = "GHI",
                ["Foo_bar"] = "Hello",
                ["Host"] = "Never",
            });

            output.AssertSuccess();
            output.AssertOutput("Hello from module!");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldShowFriendlyErrorWithInvalidSyntaxInScriptModule()
        {
            var (output, _) = RunScript("UseModule.ps1", new Dictionary<string, string>()
                {["Octopus.Script.Module[Foo]"] = "function SayHello() { Write-Host \"Hello from module! }"});

            output.AssertFailure();
            output.AssertOutput("Failed to import Script Module 'Foo'");
            output.AssertErrorOutput("Write-Host \"Hello from module!");
            output.AssertErrorOutput("The string is missing the terminator: \".");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldFailIfAModuleHasASyntaxError()
        {
            var (output, _) = RunScript("UseModule.ps1", new Dictionary<string, string>()
                {["Octopus.Script.Module[Foo]"] = "function SayHello() { Write-Host \"Hello from module! }"});

            output.AssertFailure();
            output.AssertErrorOutput("ParserError", true);
            output.AssertErrorOutput("is missing the terminator", true);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldSubstituteVariablesInNonPackagedScript()
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
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Performing variable substitution");
                output.AssertOutput("Hello Production!");
            }
        }

        [Test]
        [Description("Proves packaged scripts can have variables substituted into them before running")]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldSubstituteVariablesInPackagedScripts()
        {
            var variablesFile = Path.GetTempFileName();
            var variables = new VariableDictionary();
            variables.Set("Octopus.Environment.Name", "Production");
            variables.Set(SpecialVariables.Action.Script.ScriptFileName, "Deploy.ps1");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("package", GetFixtureResouce("Packages", "PackagedScript.1.0.0.zip"))
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Extracting package");
                output.AssertOutput("Performing variable substitution");
                output.AssertOutput("OctopusParameter: Production");
                output.AssertOutput("InlineVariable: Production");
                output.AssertOutput("VariableSubstitution: Production");
            }
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldPing()
        {
            var (output, _) = RunScript("Ping.ps1");
            output.AssertSuccess();
            output.AssertOutput("Pinging ");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldExecuteWhenPathContainsSingleQuote()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts\\Path With '", "PathWithSingleQuote.ps1")));

            output.AssertSuccess();
            output.AssertOutput("Hello from a path containing a '");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldExecuteWhenPathContainsDollar()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts\\Path With $", "PathWithDollar.ps1")));

            output.AssertSuccess();
            output.AssertOutput("Hello from a path containing a $");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldNotFailOnStdErr()
        {
            var (output, _) = RunScript("stderr.ps1");

            output.AssertSuccess();
            output.AssertErrorOutput("error");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldFailOnStdErrWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunScript("stderr.ps1", new Dictionary<string, string>()
                {["Octopus.Action.FailScriptOnErrorOutput"] = "True"});

            output.AssertFailure();
            output.AssertErrorOutput("error");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldPassOnStdInfoWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunScript("stderr.ps1", new Dictionary<string, string>()
                {["Octopus.Action.FailScriptOnErrorOutput"] = "True"});

            output.AssertSuccess();
            output.AssertOutput("Hello!");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ThrowsExceptionOnNixOrMac()
        {
            var (output, _) = RunScript("Hello.ps1");
            output.AssertErrorOutput("Powershell scripts are not supported on this platform");
        }
    }
}