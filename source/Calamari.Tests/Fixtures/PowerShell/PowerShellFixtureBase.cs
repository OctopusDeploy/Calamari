using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Tests.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PowerShell
{
    public enum PowerShellEdition
    {
        Desktop,
        Core
    }

    public abstract class PowerShellFixtureBase : CalamariFixture
    {
        protected abstract PowerShellEdition PowerShellEdition { get; }

        void AssertPowerShellEdition(CalamariResult output)
        {
            const string powerShellCoreEdition = "PSEdition                      Core";
            var trimmedOutput = output.CapturedOutput.AllMessages.Select(i => i.TrimEnd());

            if (PowerShellEdition == PowerShellEdition.Core)
                trimmedOutput.Should().Contain(powerShellCoreEdition);
            else
            {
                // Checking for not containing 'Core' as Build Servers run on
                // PowerShell 3 which does not have PSEdition in the output
                trimmedOutput.Should().NotContain(powerShellCoreEdition);
            }
        }

        [Test]
        [TestCase("true", true)]
        [TestCase("false", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void ShouldCallWithNoProfileWhenVariableSet(string executeWithoutProfile, bool calledWithNoProfile)
        {
            var variables = new CalamariVariables();
            if (executeWithoutProfile != null)
                variables.Set(PowerShellVariables.ExecuteWithoutProfile, executeWithoutProfile);

            var output = InvokeCalamariForPowerShell(calamari => calamari
                .Action("run-script")
                .Argument("script", GetFixtureResource("Scripts", ProfileScript)), 
                variables);

            Log.Verbose($"Output from invocation:{Environment.NewLine}{output}");

            output.AssertSuccess();
            // Need to check for "-NoProfile -NoLogo" not just "-NoProfile" because when
            // run via Cake we end up with the outer Powershell call included in the
            // output too, which has a -NoProfile flag.
            output.CapturedOutput.Infos
                .Any(line => line.Contains("-NoLo") && line.Contains("-NoProfile"))
                .Should().Be(calledWithNoProfile);
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldNotCallWithNoProfileWhenVariableNotSet()
        {
            var (output, _) = RunPowerShellScript(ProfileScript, new Dictionary<string, string>()
            { [PowerShellVariables.ExecuteWithoutProfile] = "true" });

            output.AssertSuccess();
            output.AssertOutput("-NoProfile");
            AssertPowerShellEdition(output);
        }

        string ProfileScript => IsRunningOnUnixLikeEnvironment ? "Profile.Nix.ps1" : "Profile.Windows.ps1";

        [Test]
        public void ShouldCallHello()
        {
            var (output, _) = RunPowerShellScript("Hello.ps1");

            output.AssertSuccess();
            output.AssertOutput("Hello!");
            AssertPowerShellEdition(output);
            output.AssertProcessNameAndId(PowerShellEdition == PowerShellEdition.Core ? "pwsh" : "powershell");
        }

        [Test]
        public void ShouldLogWarningIfScriptArgumentUsed()
        {
            var output = InvokeCalamariForPowerShell(calamari => calamari
                .Action("run-script")
                .Argument("script", GetFixtureResource("Scripts", "Hello.ps1")));

            output.AssertSuccess();
            output.AssertOutput($"##octopus[stdout-warning]{Environment.NewLine}The `--script` parameter is deprecated.");
            output.AssertOutput("Hello!");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldRetrieveCustomReturnValue()
        {
            var (output, _) = RunPowerShellScript("Exit2.ps1");

            output.AssertFailure(2);
            output.AssertOutput("Hello!");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var variables = new CalamariVariables();
            variables.Set("Name", "NameToEncrypt");

            var (output, _) = RunPowerShellScript("HelloWithVariable.ps1", new Dictionary<string, string>()
            { ["Name"] = "NameToEncrypt" }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            output.AssertSuccess();
            output.AssertOutput("Hello NameToEncrypt");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldCallHelloWithAdditionalOutputVariablesFileVariable()
        {
            if (IsRunningOnUnixLikeEnvironment)
                Assert.Inconclusive("outputVariables is provided for offline drops, and is only supported for windows deployments, since it uses DP-API to encrypt and decrypt the output variables");

            var outputVariablesFile = Path.GetTempFileName();

            var variables = new Dictionary<string, string>() { ["Octopus.Action[PreviousStep].Output.FirstName"] = "Steve" };
            var serialized = JsonConvert.SerializeObject(variables);
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(serialized), Convert.FromBase64String("5XETGOgqYR2bRhlfhDruEg=="), DataProtectionScope.CurrentUser);
            var encoded = Convert.ToBase64String(bytes);
            File.WriteAllText(outputVariablesFile, encoded);

            using (new TemporaryFile(outputVariablesFile))
            {
                var (output, _) = RunPowerShellScript("OutputVariableFromPrevious.ps1", null,
                    new Dictionary<string, string>() { ["outputVariables"] = outputVariablesFile, ["outputVariablesPassword"] = "5XETGOgqYR2bRhlfhDruEg==" });

                output.AssertSuccess();
                output.AssertOutput("Hello Steve");
                AssertPowerShellEdition(output);
            }
        }

        [Test]
        public void ShouldConsumeParametersWithQuotesUsingDeprecatedArgument()
        {
            var (output, _) = RunPowerShellScript("Parameters.ps1", additionalParameters: new Dictionary<string, string>()
            {
                ["scriptParameters"] = "-Parameter0 \"Para meter0\" -Parameter1 'Para meter1'"
            });
            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Para meter1");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunPowerShellScript("Parameters.ps1", new Dictionary<string, string>()
            {
                [SpecialVariables.Action.Script.ScriptParameters] = "-Parameter0 \"Para meter0\" -Parameter1 'Para meter1'"
            });
            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Para meter1");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldCaptureAllOutput()
        {
            var (output, _) = RunPowerShellScript("Output.ps1");
            output.AssertFailure();
            output.AssertOutput("Hello, write-host!");
            output.AssertOutput("Hello, write-output!");
            output.AssertOutput("Hello, write-verbose!");
            output.AssertOutput("Hello, write-warning!");
            output.AssertErrorOutput("Hello-Error!");
            output.AssertNoOutput("This warning should not appear in logs!");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldWriteServiceMessageForArtifacts()
        {
            var artifactPath = IsRunningOnUnixLikeEnvironment ? @"\tmp\calamari\File.txt" : @"C:\Path\File.txt";
            var (output, _) = RunPowerShellScript("CanCreateArtifact.ps1", new Dictionary<string, string> {{"ArtifactPath", artifactPath}});
            output.AssertSuccess();
            var expectedArtifactServiceMessage = IsRunningOnUnixLikeEnvironment
                ? "##octopus[createArtifact path='L3RtcC9jYWxhbWFyaS9GaWxlLnR4dA==' name='XHRtcFxjYWxhbWFyaVxGaWxlLnR4dA==' length='MA==']"
                : "##octopus[createArtifact path='QzpcUGF0aFxGaWxlLnR4dA==' name='RmlsZS50eHQ=' length='MA==']";
            output.AssertOutput(expectedArtifactServiceMessage);
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldWriteServiceMessageForUpdateProgress()
        {
            var (output, _) = RunPowerShellScript("UpdateProgress.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldWriteServiceMessageForUpdateProgressFromPipeline()
        {
            var (output, _) = RunPowerShellScript("UpdateProgressFromPipeline.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldWriteServiceMessageForPipedArtifacts()
        {
            var tempPath = Path.GetTempPath(); // There is no nice platform agnostic way to do this until powershell 7 ships and is on all our test agents (this introduces a new "TEMP" drive)
            var artifacts = Enumerable.Range(0, 3).Select(i =>
                new Artifact(Path.Combine(tempPath, $"CanCreateArtifactPipedTestFile{i}.artifact"))).ToList();
            foreach (var artifact in artifacts)
            {
                artifact.Create();
            }

            try
            {
                var (output, _) = RunPowerShellScript("CanCreateArtifactPiped.ps1", new Dictionary<string, string> {{"TempDirectory", tempPath}});
                output.AssertSuccess();
                foreach (var artifact in artifacts)
                {
                    var expectedPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(artifact.Path));
                    var expectedName = Convert.ToBase64String(Encoding.UTF8.GetBytes(artifact.Name));
                    output.AssertOutput($"##octopus[createArtifact path='{expectedPath}' name='{expectedName}' length='MA==']");
                }

                AssertPowerShellEdition(output);
            }
            finally
            {
                foreach (var artifact in artifacts)
                {
                    artifact.Delete();
                }
            }
        }

        class Artifact
        {
            public string Name => System.IO.Path.GetFileName(Path);
            public string Path { get; }

            public Artifact(string path)
            {
                Path = path;
            }
            public void Create()
            {
                if (!File.Exists(Path))
                    File.WriteAllText(Path, "");
            }

            public void Delete()
            {
                File.Delete(Path);
            }
        }

        [Test]
        public void ShouldWriteVerboseMessageForArtifactsThatDoNotExist()
        {
            var nonExistantArtifactPath = IsRunningOnUnixLikeEnvironment
                ? @"\tmp\NonExistantPath\NonExistantFile.txt"
                : @"C:\NonExistantPath\NonExistantFile.txt";
            var (output, _) = RunPowerShellScript("WarningForMissingArtifact.ps1", new Dictionary<string, string> {{"ArtifactPath", nonExistantArtifactPath}});
            output.AssertSuccess();
            output.AssertOutput($@"There is no file at '{nonExistantArtifactPath}' right now. Writing the service message just in case the file is available when the artifacts are collected at a later point in time.");
            var expectedArtifactServiceMessage = IsRunningOnUnixLikeEnvironment
                ? "##octopus[createArtifact path='L3RtcC9Ob25FeGlzdGFudFBhdGgvTm9uRXhpc3RhbnRGaWxlLnR4dA==' name='XHRtcFxOb25FeGlzdGFudFBhdGhcTm9uRXhpc3RhbnRGaWxlLnR4dA==' length='MA==']"
                : "##octopus[createArtifact path='QzpcTm9uRXhpc3RhbnRQYXRoXE5vbkV4aXN0YW50RmlsZS50eHQ=' name='Tm9uRXhpc3RhbnRGaWxlLnR4dA==' length='MA==']";
            output.AssertOutput(expectedArtifactServiceMessage);
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldAllowDotSourcing()
        {
            var output = InvokeCalamariForPowerShell(calamari => calamari
                .Action("run-script")
                .Argument("script", GetFixtureResource("Scripts", "CanDotSource.ps1")));

            output.AssertSuccess();
            output.AssertOutput("Hello!");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldSetVariables()
        {
            var (output, variables) = RunPowerShellScript("CanSetVariable.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='VGVzdEE=' value='V29ybGQh']");
            Assert.AreEqual("World!", variables.Get("TestA"));
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldSetSensitiveVariables()
        {
            var (output, variables) = RunPowerShellScript("CanSetVariable.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='U2VjcmV0U3F1aXJyZWw=' value='WCBtYXJrcyB0aGUgc3BvdA==' sensitive='VHJ1ZQ==']");
            Assert.AreEqual("X marks the spot", variables.Get("SecretSquirrel"));
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldSetActionIndexedOutputVariables()
        {
            var (output, variables) = RunPowerShellScript("CanSetVariable.ps1", new Dictionary<string, string>
            {
                [ActionVariables.Name] = "run-script"
            });
            Assert.AreEqual("World!", variables.Get("Octopus.Action[run-script].Output.TestA"));
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldSetMachineIndexedOutputVariables()
        {
            var (output, variables) = RunPowerShellScript("CanSetVariable.ps1", new Dictionary<string, string>
            {
                [ActionVariables.Name] = "run-script",
                [MachineVariables.Name] = "App01"
            });

            Assert.AreEqual("World!", variables.Get("Octopus.Action[run-script].Output[App01].TestA"));
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldFailOnInvalid()
        {
            var (output, _) = RunPowerShellScript("Invalid.ps1");
            output.AssertFailure();
            output.AssertErrorOutput("A positional parameter cannot be found that accepts");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldFailOnInvalidSyntax()
        {
            var (output, _) = RunPowerShellScript("InvalidSyntax.ps1");

            output.AssertFailure();
            //ensure it logs the type of error
            output.AssertErrorOutput("ParserError:");
            //ensure it logs the error line in question
            output.AssertErrorOutput("+ $#FC(@UCJ@(#U");
            //ensure it logs each of the errors
            output.AssertErrorOutput("Unexpected token '@(' in expression or statement.");
            output.AssertErrorOutput("Missing closing ')' in expression.");
            output.AssertErrorOutput("The splatting operator '@' cannot be used to reference variables in an expression. '@UCJ' can be used only as an argument to a command. To reference variables in an expression use '$UCJ'.");
            //ensure it logs the stack trace
            output.AssertErrorOutput("at <ScriptBlock>, ");

            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldPrintVariables()
        {
            var (output, _) = RunPowerShellScript("PrintVariables.ps1", new Dictionary<string, string>
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
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldSupportModulesInVariables()
        {
            var (output, _) = RunPowerShellScript("UseModule.ps1", new Dictionary<string, string>
            {
                ["Octopus.Script.Module[Foo]"] = "function SayHello() { Write-Host \"Hello from module!\" }",
                ["Variable2"] = "DEF",
                ["Variable3"] = "GHI",
                ["Foo_bar"] = "Hello",
                ["Host"] = "Never",
            });

            output.AssertSuccess();
            output.AssertOutput("Hello from module!");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldShowFriendlyErrorWithInvalidSyntaxInScriptModule()
        {
            var (output, _) = RunPowerShellScript("UseModule.ps1", new Dictionary<string, string>()
            { ["Octopus.Script.Module[Foo]"] = "function SayHello() { Write-Host \"Hello from module! }" });

            output.AssertFailure();
            //ensure it logs the script module name
            output.AssertErrorOutput("Failed to import Script Module 'Foo' from '");
            //ensure it logs the type of error
            output.AssertErrorOutput("ParserError:");
            //ensure it logs the error line in question
            output.AssertErrorOutput("+ function SayHello() { Write-Host \"Hello from module! }");
            //ensure it logs each of the errors
            output.AssertErrorOutput("The string is missing the terminator: \".");
            output.AssertErrorOutput("Missing closing '}' in statement block");
            //ensure it logs the stack trace
            output.AssertErrorOutput("at Import-ScriptModule, ");
            output.AssertErrorOutput("at <ScriptBlock>, ");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldFailIfAModuleHasASyntaxError()
        {
            var (output, _) = RunPowerShellScript("UseModule.ps1", new Dictionary<string, string>()
            { ["Octopus.Script.Module[Foo]"] = "function SayHello() { Write-Host \"Hello from module! }" });

            output.AssertFailure();
            output.AssertErrorOutput("ParserError", true);
            output.AssertErrorOutput("is missing the terminator", true);
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldNotSubstituteVariablesInNonPackagedScript()
        {
            // Use a temp file for the script to avoid mutating the script file for other tests
            var scriptFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ps1");
            File.WriteAllText(scriptFile, "Write-Host \"Hello #{Octopus.Environment.Name}!\"");

            var variables = new CalamariVariables();
            variables.Set("Octopus.Environment.Name", "Production");

            using (new TemporaryFile(scriptFile))
            {
                var output = InvokeCalamariForPowerShell(calamari => calamari
                    .Action("run-script")
                    .Argument("script", scriptFile), variables);

                output.AssertSuccess();
                output.AssertOutput("Hello #{Octopus.Environment.Name}!");
                AssertPowerShellEdition(output);
            }
        }

        [Test]
        [Description("Proves packaged scripts can have variables substituted into them before running")]
        public void ShouldSubstituteVariablesInPackagedScripts()
        {
            var variables = new CalamariVariables();
            variables.Set("Octopus.Environment.Name", "Production");
            variables.Set(ScriptVariables.ScriptFileName, "Deploy.ps1");

            var output = InvokeCalamariForPowerShell(calamari => calamari
                .Action("run-script")
                .Argument("package", GetFixtureResource("Packages", "PackagedScript.1.0.0.zip")), variables);

            output.AssertSuccess();
            output.AssertOutput("Extracting package");
            output.AssertOutput("Performing variable substitution");
            output.AssertOutput("OctopusParameter: Production");
            output.AssertOutput("InlineVariable: Production");
            output.AssertOutput("VariableSubstitution: Production");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldPing()
        {
            var pingScriptName = IsRunningOnUnixLikeEnvironment
                ? "Ping.Nix.ps1"
                : "Ping.Win.ps1";
            var (output, _) = RunPowerShellScript(pingScriptName);
            output.AssertSuccess();

            var expectedPingingText = IsRunningOnUnixLikeEnvironment ? "PING " : "Pinging ";
            output.AssertOutput(expectedPingingText);
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldExecuteWhenPathContainsSingleQuote()
        {
            var output = InvokeCalamariForPowerShell(calamari => calamari
                .Action("run-script")
                .Argument("script", GetFixtureResource(Path.Combine("Scripts", "Path With '"), "PathWithSingleQuote.ps1")));

            output.AssertSuccess();
            output.AssertOutput("Hello from a path containing a '");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldExecuteWhenPathContainsDollar()
        {
            var output = InvokeCalamariForPowerShell(calamari => calamari
                .Action("run-script")
                .Argument("script", GetFixtureResource(Path.Combine("Scripts", "Path With $"), "PathWithDollar.ps1")));

            output.AssertSuccess();
            output.AssertOutput("Hello from a path containing a $");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldNotFailOnStdErr()
        {
            var (output, _) = RunPowerShellScript("StdErr.ps1");

            output.AssertSuccess();
            output.AssertErrorOutput("error");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldFailOnStdErrWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunPowerShellScript("StdErr.ps1", new Dictionary<string, string>()
            { ["Octopus.Action.FailScriptOnErrorOutput"] = "True" });

            output.AssertFailure();
            output.AssertErrorOutput("error");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldPassOnStdInfoWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunPowerShellScript("Hello.ps1", new Dictionary<string, string>()
            { ["Octopus.Action.FailScriptOnErrorOutput"] = "True" });

            output.AssertSuccess();
            output.AssertOutput("Hello!");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void ShouldNotDoubleReplaceVariables()
        {
            var (output, _) = RunPowerShellScript("DontDoubleReplace.ps1", new Dictionary<string, string>()
            { ["Octopus.Machine.Name"] = "Foo" });

            output.AssertSuccess();
            output.AssertOutput("The  Octopus variable for machine name is #{Octopus.Machine.Name}");
            output.AssertOutput("An example of this evaluated is: 'Foo'");
            AssertPowerShellEdition(output);
        }

        [Test]
        public void CharacterWithBomMarkCorrectlyEncoded()
        {
            var (output, _) = RunPowerShellScript("ScriptWithBOM.ps1");

            output.AssertSuccess();
            output.AssertOutput(string.Join(Environment.NewLine, "45", "226", "128", "147"));
            AssertPowerShellEdition(output);
        }

        static bool IsRunningOnUnixLikeEnvironment => CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac;

        protected CalamariResult InvokeCalamariForPowerShell(Action<CommandLine> buildCommand, CalamariVariables variables = null)
        {
            var variableDictionary = variables ?? new CalamariVariables();
            variableDictionary.Add(PowerShellVariables.Edition, GetPowerShellEditionVariable());

            using (var variablesFile = CreateVariablesFile(variableDictionary))
            {
                var calamariCommand = Calamari();
                buildCommand(calamariCommand);
                calamariCommand.Argument("variables", variablesFile.FilePath);
                return Invoke(calamariCommand);
            }
        }

        VariableFile CreateVariablesFile(CalamariVariables variables)
        {
            return new VariableFile(variables);
        }

        class VariableFile : IDisposable
        {
            readonly TemporaryFile tempFile;
            public string FilePath { get; }

            public VariableFile(CalamariVariables variables)
            {
                FilePath = Path.GetTempFileName();
                tempFile = new TemporaryFile(FilePath);
                variables.Save(FilePath);
            }

            public void Dispose()
            {
                tempFile.Dispose();
            }
        }

        (CalamariResult result, IVariables variables) RunPowerShellScript(string scriptName,
            Dictionary<string, string> additionalVariables = null,
            Dictionary<string, string> additionalParameters = null,
            string sensitiveVariablesPassword = null)
        {
            var variablesDictionary = additionalVariables ?? new Dictionary<string, string>();
            variablesDictionary.Add(PowerShellVariables.Edition, GetPowerShellEditionVariable());
            return RunScript(scriptName, variablesDictionary, additionalParameters, sensitiveVariablesPassword);
        }

        string GetPowerShellEditionVariable()
        {
            switch(PowerShellEdition)
            {
                case PowerShellEdition.Desktop:
                    return "Desktop";
                case PowerShellEdition.Core:
                    return "Core";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}