﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.PowerShell
{    
    [TestFixture]
    [Category(TestCategory.CompatibleOS.Windows)]
    public class WindowsPowerShellFixture : PowerShellFixture
    {
        [Test]
        [Platform]
        // Windows 2016 (has PowerShell 2) will also match Windows 2019 (no PowerShell 2) so have omitted it.
        [TestCase("2", "PSVersion                      2.0", IncludePlatform = "Win2008Server,Win2008ServerR2,Win2012Server,Win2012ServerR2,Windows10")]
        [TestCase("2.0", "PSVersion                      2.0", IncludePlatform = "Win2008Server,Win2008ServerR2,Win2012Server,Win2012ServerR2,Windows10")]
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

                if (output.CapturedOutput.AllMessages
                    .Select(line => new string(line.ToCharArray().Where(c => c != '\u0000').ToArray()))
                    .Any(line => line.Contains(".NET Framework is not installed")))
                {
                    Assert.Inconclusive("Version 2.0 of PowerShell is not supported on this machine");
                }
                
                output.AssertSuccess();
                output.AssertOutput(expectedLogMessage);
                output.AssertOutput("Hello!");
            }
        }
        
        [Test]
        public void ShouldPrioritizePowershellScriptsOverOtherSyntaxes()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.PowerShell), "Write-Host Hello Powershell");
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.CSharp), "Write-Host Hello CSharp");
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.Bash), "echo Hello Bash");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Hello Powershell");
            }
        }
    }
    
    [TestFixture]
    [Category(TestCategory.CompatibleOS.Nix)]
    public class WindowsPowerShellOnLinuxFixture : CalamariFixture
    {
        [Test]
        public void PowerShellThrowsExceptionOnNix()
        {
            var (output, _) = RunScript("Hello.ps1");
            output.AssertErrorOutput("PowerShell scripts are not supported on this platform");
        }

        [Test]
        public void ShouldRunBashInsteadOfPowerShell()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.PowerShell), "Write-Host Hello Powershell");
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.CSharp), "Write-Host Hello CSharp");
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.Bash), "echo Hello Bash");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Hello Bash");
            }
        }
    }

    [TestFixture]
    [Category(TestCategory.CompatibleOS.Mac)]
    public class WindowsPowerShellOnMacFixture : CalamariFixture
    {
        [Test]
        public void PowerShellThrowsExceptionOnMac()
        {
            var (output, _) = RunScript("Hello.ps1");
            output.AssertErrorOutput("PowerShell scripts are not supported on this platform");
        }

        [Test]
        public void ShouldRunBashInsteadOfPowerShell()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.PowerShell), "Write-Host Hello Powershell");
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.CSharp), "Write-Host Hello CSharp");
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.Bash), "echo Hello Bash");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Hello Bash");
            }
        }
    }
    
    public abstract class PowerShellFixture : CalamariFixture
    {
        void AssertPSEdition(CalamariResult output)
        {
            // Checking for not containing 'Core' as Build Servers run on
            // PowerShell 3 which does not have PSEdition in the output
            output.CapturedOutput.AllMessages.Select(i => i.TrimEnd()).Should()
                .NotContain($"PSEdition                      Core");
        }

        [Test]
        [TestCase("true", true)]
        [TestCase("false", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void ShouldCallWithNoProfileWhenVariableSet(string executeWithoutProfile, bool calledWithNoProfile)
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            if (executeWithoutProfile != null)
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
                AssertPSEdition(output);
            }
        }

        [Test]
        public void ShouldNotCallWithNoProfileWhenVariableNotSet()
        {
            var (output, _) = RunScript("Profile.ps1", new Dictionary<string, string>()
            { [SpecialVariables.Action.PowerShell.ExecuteWithoutProfile] = "true" });

            output.AssertSuccess();
            output.AssertOutput("-NoProfile");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldCallHello()
        {
            var (output, _) = RunScript("Hello.ps1");

            output.AssertSuccess();
            output.AssertOutput("Hello!");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldLogWarningIfScriptArgumentUsed()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "Hello.ps1")));

            output.AssertSuccess();
            output.AssertOutput("##octopus[stdout-warning]\r\nThe `--script` parameter is deprecated.");
            output.AssertOutput("Hello!");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldRetrieveCustomReturnValue()
        {
            var (output, _) = RunScript("Exit2.ps1");

            output.AssertFailure(2);
            output.AssertOutput("Hello!");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Name", "NameToEncrypt");
            variables.SaveEncrypted("5XETGOgqYR2bRhlfhDruEg==", variablesFile);

            var (output, _) = RunScript("HelloWithVariable.ps1", new Dictionary<string, string>()
            { ["Name"] = "NameToEncrypt" }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            output.AssertSuccess();
            output.AssertOutput("Hello NameToEncrypt");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldCallHelloWithAdditionalOutputVariablesFileVariable()
        {
            var outputVariablesFile = Path.GetTempFileName();

            var variables = new Dictionary<string, string>() { ["Octopus.Action[PreviousStep].Output.FirstName"] = "Steve" };
            var serialized = JsonConvert.SerializeObject(variables);
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(serialized), Convert.FromBase64String("5XETGOgqYR2bRhlfhDruEg=="), DataProtectionScope.CurrentUser);
            var encoded = Convert.ToBase64String(bytes);
            File.WriteAllText(outputVariablesFile, encoded);

            using (new TemporaryFile(outputVariablesFile))
            {
                var (output, _) = RunScript("OutputVariableFromPrevious.ps1", null,
                    new Dictionary<string, string>() { ["outputVariables"] = outputVariablesFile, ["outputVariablesPassword"] = "5XETGOgqYR2bRhlfhDruEg==" });

                output.AssertSuccess();
                output.AssertOutput("Hello Steve");
                AssertPSEdition(output);
            }
        }

        [Test]
        public void ShouldConsumeParametersWithQuotesUsingDeprecatedArgument()
        {
            var (output, _) = RunScript("Parameters.ps1", additionalParameters: new Dictionary<string, string>()
            {
                ["scriptParameters"] = "-Parameter0 \"Para meter0\" -Parameter1 'Para meter1'"
            });
            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Para meter1");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunScript("Parameters.ps1", new Dictionary<string, string>()
            {
                [SpecialVariables.Action.Script.ScriptParameters] = "-Parameter0 \"Para meter0\" -Parameter1 'Para meter1'"
            });
            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Para meter1");
            AssertPSEdition(output);
        }

        [Test]
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
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldWriteServiceMessageForArtifacts()
        {
            var (output, _) = RunScript("CanCreateArtifact.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact path='QzpcUGF0aFxGaWxlLnR4dA==' name='RmlsZS50eHQ=' length='MA==']");
            AssertPSEdition(output);
        }
        
        [Test]
        public void ShouldWriteServiceMessageForUpdateProgress()
        {
            var (output, _) = RunScript("UpdateProgress.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
            AssertPSEdition(output);
        }
        
        [Test]
        public void ShouldWriteServiceMessageForUpdateProgressFromPipeline()
        {
            var (output, _) = RunScript("UpdateProgressFromPipeline.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldWriteServiceMessageForPipedArtifacts()
        {
            var path = Path.Combine(Path.GetTempPath(), "CanCreateArtifactPipedTestFile.txt");
            var base64Path = Convert.ToBase64String(Encoding.UTF8.GetBytes(path));
            try
            {
                if (!File.Exists(path))
                    File.WriteAllText(path, "");
                var (output, _) = RunScript("CanCreateArtifactPiped.ps1");
                output.AssertSuccess();
                output.AssertOutput($"##octopus[createArtifact path='{base64Path}' name='Q2FuQ3JlYXRlQXJ0aWZhY3RQaXBlZFRlc3RGaWxlLnR4dA==' length='MA==']");
                AssertPSEdition(output);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void ShouldWriteVerboseMessageForArtifactsThatDoNotExist()
        {
            var (output, _) = RunScript("WarningForMissingArtifact.ps1");
            output.AssertSuccess();
            output.AssertOutput(@"There is no file at 'C:\NonExistantPath\NonExistantFile.txt' right now. Writing the service message just in case the file is available when the artifacts are collected at a later point in time.");
            output.AssertOutput("##octopus[createArtifact path='QzpcTm9uRXhpc3RhbnRQYXRoXE5vbkV4aXN0YW50RmlsZS50eHQ=' name='Tm9uRXhpc3RhbnRGaWxlLnR4dA==' length='MA==']");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldAllowDotSourcing()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "CanDotSource.ps1")));

            output.AssertSuccess();
            output.AssertOutput("Hello!");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldSetVariables()
        {
            var (output, variables) = RunScript("CanSetVariable.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='VGVzdEE=' value='V29ybGQh']");
            Assert.AreEqual("World!", variables.Get("TestA"));
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldSetSensitiveVariables()
        {
            var (output, variables) = RunScript("CanSetVariable.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='U2VjcmV0U3F1aXJyZWw=' value='WCBtYXJrcyB0aGUgc3BvdA==' sensitive='VHJ1ZQ==']");
            Assert.AreEqual("X marks the spot", variables.Get("SecretSquirrel"));
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldSetActionIndexedOutputVariables()
        {
            var (output, variables) = RunScript("CanSetVariable.ps1", new Dictionary<string, string>
            {
                [SpecialVariables.Action.Name] = "run-script"
            });
            Assert.AreEqual("World!", variables.Get("Octopus.Action[run-script].Output.TestA"));
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldSetMachineIndexedOutputVariables()
        {
            var (output, variables) = RunScript("CanSetVariable.ps1", new Dictionary<string, string>
            {
                [SpecialVariables.Action.Name] = "run-script",
                [SpecialVariables.Machine.Name] = "App01"
            });

            Assert.AreEqual("World!", variables.Get("Octopus.Action[run-script].Output[App01].TestA"));
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldFailOnInvalid()
        {
            var (output, _) = RunScript("Invalid.ps1");
            output.AssertFailure();
            output.AssertErrorOutput("A positional parameter cannot be found that accepts");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldFailOnInvalidSyntax()
        {
            var (output, _) = RunScript("InvalidSyntax.ps1");
            output.AssertFailure();
            output.AssertErrorOutput("ParserError");
            AssertPSEdition(output);
        }

        [Test]
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
            AssertPSEdition(output);
        }

        [Test]
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
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldShowFriendlyErrorWithInvalidSyntaxInScriptModule()
        {
            var (output, _) = RunScript("UseModule.ps1", new Dictionary<string, string>()
            { ["Octopus.Script.Module[Foo]"] = "function SayHello() { Write-Host \"Hello from module! }" });

            output.AssertFailure();
            output.AssertOutput("Failed to import Script Module 'Foo'");
            output.AssertErrorOutput("Write-Host \"Hello from module!");
            output.AssertErrorOutput("The string is missing the terminator: \".");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldFailIfAModuleHasASyntaxError()
        {
            var (output, _) = RunScript("UseModule.ps1", new Dictionary<string, string>()
            { ["Octopus.Script.Module[Foo]"] = "function SayHello() { Write-Host \"Hello from module! }" });

            output.AssertFailure();
            output.AssertErrorOutput("ParserError", true);
            output.AssertErrorOutput("is missing the terminator", true);
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldNotSubstituteVariablesInNonPackagedScript()
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
                output.AssertOutput("Hello #{Octopus.Environment.Name}!");
                AssertPSEdition(output);
            }
        }

        [Test]
        [Description("Proves packaged scripts can have variables substituted into them before running")]
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
                AssertPSEdition(output);
            }
        }

        [Test]
        public void ShouldPing()
        {
            var (output, _) = RunScript("Ping.ps1");
            output.AssertSuccess();
            output.AssertOutput("Pinging ");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldExecuteWhenPathContainsSingleQuote()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts\\Path With '", "PathWithSingleQuote.ps1")));

            output.AssertSuccess();
            output.AssertOutput("Hello from a path containing a '");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldExecuteWhenPathContainsDollar()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts\\Path With $", "PathWithDollar.ps1")));

            output.AssertSuccess();
            output.AssertOutput("Hello from a path containing a $");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldNotFailOnStdErr()
        {
            var (output, _) = RunScript("stderr.ps1");

            output.AssertSuccess();
            output.AssertErrorOutput("error");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldFailOnStdErrWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunScript("stderr.ps1", new Dictionary<string, string>()
            { ["Octopus.Action.FailScriptOnErrorOutput"] = "True" });

            output.AssertFailure();
            output.AssertErrorOutput("error");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldPassOnStdInfoWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunScript("Hello.ps1", new Dictionary<string, string>()
            { ["Octopus.Action.FailScriptOnErrorOutput"] = "True" });

            output.AssertSuccess();
            output.AssertOutput("Hello!");
            AssertPSEdition(output);
        }

        [Test]
        public void ShouldNotDoubleReplaceVariables()
        {
            var (output, _) = RunScript("DontDoubleReplace.ps1", new Dictionary<string, string>()
            { ["Octopus.Machine.Name"] = "Foo" });

            output.AssertSuccess();
            output.AssertOutput("The  Octopus variable for machine name is #{Octopus.Machine.Name}");
            output.AssertOutput("An example of this evaluated is: 'Foo'");
            AssertPSEdition(output);
        }

        [Test]
        public void CharacterWithBomMarkCorrectlyEncoded()
        {
            var (output, _) = RunScript("ScriptWithBOM.ps1");

            output.AssertSuccess();
            output.AssertOutput("45\r\n226\r\n128\r\n147");
            AssertPSEdition(output);
        }
    }
}