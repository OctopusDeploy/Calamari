using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using Calamari.Util.Environments;
using Newtonsoft.Json;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.PowerShell
{
    [TestFixture]
    public class PowerShellFixture : CalamariFixture
    {
        [TearDown]
        public void Teardown()
        {
            ResetProxyEnvironmentVariables();
        }

        [Test]
        [Platform]
        [Category(TestCategory.CompatibleOS.Windows)]
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

                output.AssertSuccess();
                output.AssertOutput(expectedLogMessage);
                output.AssertOutput("Hello!");
            }
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
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
            }
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldNotCallWithNoProfileWhenVariableNotSet()
        {
            var (output, _) = RunScript("Profile.ps1", new Dictionary<string, string>()
            { [SpecialVariables.Action.PowerShell.ExecuteWithoutProfile] = "true" });

            output.AssertSuccess();
            output.AssertOutput("-NoProfile");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldCallHello()
        {
            var (output, _) = RunScript("Hello.ps1");

            output.AssertSuccess();
            output.AssertOutput("Hello!");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldLogWarningIfScriptArgumentUsed()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "Hello.ps1")));

            output.AssertSuccess();
            output.AssertOutput("##octopus[stdout-warning]\r\nThe `--script` parameter is deprecated.");
            output.AssertOutput("Hello!");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldRetrieveCustomReturnValue()
        {
            var (output, _) = RunScript("Exit2.ps1");

            output.AssertFailure(2);
            output.AssertOutput("Hello!");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
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
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
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
            }
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldConsumeParametersWithQuotesUsingDeprecatedArgument()
        {
            var (output, _) = RunScript("Parameters.ps1", additionalParameters: new Dictionary<string, string>()
            {
                ["scriptParameters"] = "-Parameter0 \"Para meter0\" -Parameter1 'Para meter1'"
            });
            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Para meter1");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunScript("Parameters.ps1", new Dictionary<string, string>()
            {
                [SpecialVariables.Action.Script.ScriptParameters] = "-Parameter0 \"Para meter0\" -Parameter1 'Para meter1'"
            });
            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Para meter1");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
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
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldWriteServiceMessageForArtifacts()
        {
            var (output, _) = RunScript("CanCreateArtifact.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact path='QzpcUGF0aFxGaWxlLnR4dA==' name='RmlsZS50eHQ=' length='MA==']");
            //  this.Assent(output.CapturedOutput.ToApprovalString(), new Configuration().UsingNamer(new SubdirectoryNamer("Approved")));
        }
        
        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldWriteServiceMessageForUpdateProgress()
        {
            var (output, _) = RunScript("UpdateProgress.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
        }
        
        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldWriteServiceMessageForUpdateProgressFromPipeline()
        {
            var (output, _) = RunScript("UpdateProgressFromPipeline.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
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
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        
        public void ShouldWriteVerboseMessageForArtifactsThatDoNotExist()
        {
            var (output, _) = RunScript("WarningForMissingArtifact.ps1");
            output.AssertSuccess();
            output.AssertOutput(@"There is no file at 'C:\NonExistantPath\NonExistantFile.txt' right now. Writing the service message just in case the file is available when the artifacts are collected at a later point in time.");
            output.AssertOutput("##octopus[createArtifact path='QzpcTm9uRXhpc3RhbnRQYXRoXE5vbkV4aXN0YW50RmlsZS50eHQ=' name='Tm9uRXhpc3RhbnRGaWxlLnR4dA==' length='MA==']");
            // this.Assent(output.CapturedOutput.ToApprovalString(), new Configuration().UsingNamer(new SubdirectoryNamer("Approved")));
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldAllowDotSourcing()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "CanDotSource.ps1")));

            output.AssertSuccess();
            output.AssertOutput("Hello!");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldSetVariables()
        {
            var (output, variables) = RunScript("CanSetVariable.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='VGVzdEE=' value='V29ybGQh']");
            Assert.AreEqual("World!", variables.Get("TestA"));
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldSetSensitiveVariables()
        {
            var (output, variables) = RunScript("CanSetVariable.ps1");
            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='U2VjcmV0U3F1aXJyZWw=' value='WCBtYXJrcyB0aGUgc3BvdA==' sensitive='VHJ1ZQ==']");
            Assert.AreEqual("X marks the spot", variables.Get("SecretSquirrel"));
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldSetActionIndexedOutputVariables()
        {
            var (_, variables) = RunScript("CanSetVariable.ps1", new Dictionary<string, string>
            {
                [SpecialVariables.Action.Name] = "run-script"
            });
            Assert.AreEqual("World!", variables.Get("Octopus.Action[run-script].Output.TestA"));
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
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
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldFailOnInvalid()
        {
            var (output, _) = RunScript("Invalid.ps1");
            output.AssertFailure();
            output.AssertErrorOutput("A positional parameter cannot be found that accepts");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldFailOnInvalidSyntax()
        {
            var (output, _) = RunScript("InvalidSyntax.ps1");
            output.AssertFailure();
            output.AssertErrorOutput("ParserError");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
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
        [Category(TestCategory.CompatibleOS.Windows)]
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
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldShowFriendlyErrorWithInvalidSyntaxInScriptModule()
        {
            var (output, _) = RunScript("UseModule.ps1", new Dictionary<string, string>()
            { ["Octopus.Script.Module[Foo]"] = "function SayHello() { Write-Host \"Hello from module! }" });

            output.AssertFailure();
            output.AssertOutput("Failed to import Script Module 'Foo'");
            output.AssertErrorOutput("Write-Host \"Hello from module!");
            output.AssertErrorOutput("The string is missing the terminator: \".");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldFailIfAModuleHasASyntaxError()
        {
            var (output, _) = RunScript("UseModule.ps1", new Dictionary<string, string>()
            { ["Octopus.Script.Module[Foo]"] = "function SayHello() { Write-Host \"Hello from module! }" });

            output.AssertFailure();
            output.AssertErrorOutput("ParserError", true);
            output.AssertErrorOutput("is missing the terminator", true);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
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
            }
        }

        [Test]
        [Description("Proves packaged scripts can have variables substituted into them before running")]
        [Category(TestCategory.CompatibleOS.Windows)]
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
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldPing()
        {
            var (output, _) = RunScript("Ping.ps1");
            output.AssertSuccess();
            output.AssertOutput("Pinging ");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldExecuteWhenPathContainsSingleQuote()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts\\Path With '", "PathWithSingleQuote.ps1")));

            output.AssertSuccess();
            output.AssertOutput("Hello from a path containing a '");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldExecuteWhenPathContainsDollar()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts\\Path With $", "PathWithDollar.ps1")));

            output.AssertSuccess();
            output.AssertOutput("Hello from a path containing a $");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldNotFailOnStdErr()
        {
            var (output, _) = RunScript("stderr.ps1");

            output.AssertSuccess();
            output.AssertErrorOutput("error");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldFailOnStdErrWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunScript("stderr.ps1", new Dictionary<string, string>()
            { ["Octopus.Action.FailScriptOnErrorOutput"] = "True" });

            output.AssertFailure();
            output.AssertErrorOutput("error");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldPassOnStdInfoWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunScript("Hello.ps1", new Dictionary<string, string>()
            { ["Octopus.Action.FailScriptOnErrorOutput"] = "True" });

            output.AssertSuccess();
            output.AssertOutput("Hello!");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ShouldNotDoubleReplaceVariables()
        {
            var (output, _) = RunScript("DontDoubleReplace.ps1", new Dictionary<string, string>()
            { ["Octopus.Machine.Name"] = "Foo" });

            output.AssertSuccess();
            output.AssertOutput("The  Octopus variable for machine name is #{Octopus.Machine.Name}");
            output.AssertOutput("An example of this evaluated is: 'Foo'");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Nix)]
        [Category(TestCategory.CompatibleOS.Mac)]
        public void PowershellThrowsExceptionOnNixOrMac()
        {
            var (output, _) = RunScript("Hello.ps1");
            output.AssertErrorOutput("PowerShell scripts are not supported on this platform");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void CharacterWithBomMarkCorrectlyEncoded()
        {
            var (output, _) = RunScript("ScriptWithBOM.ps1");

            output.AssertSuccess();
            output.AssertOutput("45\r\n226\r\n128\r\n147");
        }


        [Test]
        public void ShouldAllowPlatformSpecificScriptToExecute()
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
                output.AssertOutput(CalamariEnvironment.IsRunningOnWindows ? "Hello Powershell" : "Hello Bash");
            }
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ProxyNotSet_ShouldNotSetEnvironmentVariables()
        {
            ResetProxyEnvironmentVariables();

            EnvironmentHelper.SetEnvironmentVariable("TentacleUseDefaultProxy", false.ToString());
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", "");

            var (output, _) = RunScript("Proxy.ps1");

            output.AssertSuccess();
            output.AssertNoOutput($"Setting Proxy Environment Variables");
            output.AssertOutputContains($"HTTP_PROXY: ");
            output.AssertOutputContains($"HTTPS_PROXY: ");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ProxyConfigured_ShouldSetEnvironmentVariables()
        {
            ResetProxyEnvironmentVariables();

            var proxyHost = "hostname";
            var proxyPort = "3456";
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", proxyHost);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyPort", proxyPort);

            var (output, _) = RunScript("Proxy.ps1");

            output.AssertSuccess();
            output.AssertOutputContains($"HTTP_PROXY: http://{proxyHost}:{proxyPort}");
            output.AssertOutputContains($"HTTPS_PROXY: http://{proxyHost}:{proxyPort}");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ProxyWithAuthConfigured_ShouldSetEnvironmentVariables()
        {
            ResetProxyEnvironmentVariables();

            var proxyHost = "hostname";
            var proxyPort = "3456";
            var proxyUsername = "username";
            var proxyPassword = "password";
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", proxyHost);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyPort", proxyPort);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyUsername", proxyUsername);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyPassword", proxyPassword);

            var (output, _) = RunScript("Proxy.ps1");

            output.AssertSuccess();
            output.AssertOutputContains($"HTTP_PROXY: http://{proxyUsername}:{proxyPassword}@{proxyHost}:{proxyPort}");
            output.AssertOutputContains($"HTTPS_PROXY: http://{proxyUsername}:{proxyPassword}@{proxyHost}:{proxyPort}");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ProxyEnvironmentAlreadyConfigured_ShouldSetNotSetVariables()
        {
            ResetProxyEnvironmentVariables();

            var proxyHost = "hostname";
            var proxyPort = "3456";
            var httpProxy = "http://proxy:port";
            var httpsProxy = "http://proxy2:port";
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", proxyHost);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyPort", proxyPort);
            EnvironmentHelper.SetEnvironmentVariable("HTTP_PROXY", httpProxy);
            EnvironmentHelper.SetEnvironmentVariable("HTTPS_PROXY", httpsProxy);

            var (output, _) = RunScript("Proxy.ps1");

            output.AssertSuccess();
            output.AssertOutputContains($"HTTP_PROXY: {httpProxy}");
            output.AssertOutputContains($"HTTPS_PROXY: {httpsProxy}");

            ResetProxyEnvironmentVariables();
        }

#if NETFX
        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ProxySetToSystem_ShouldSetVariablesCorrectly()
        {
            ResetProxyEnvironmentVariables();

            var (output, _) = RunScript("Proxy.ps1");
            var systemProxyUri = System.Net.WebRequest.GetSystemWebProxy().GetProxy(new Uri(@"https://octopus.com"));
            if (systemProxyUri.Host == "octopus.com")
            {
                output.AssertSuccess();
                output.AssertOutputContains($"HTTP_PROXY: ");
                output.AssertOutputContains($"HTTPS_PROXY: ");
            }
            else
            {
                output.AssertSuccess();
                output.AssertOutputContains($"HTTP_PROXY: http://{systemProxyUri.Host}:{systemProxyUri.Port}");
                output.AssertOutputContains($"HTTPS_PROXY: http://{systemProxyUri.Host}:{systemProxyUri.Port}");
            }
        }
#endif

#if NETFX
        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ProxyNoConfig_ShouldSetNotSetVariables()
        {
            ResetProxyEnvironmentVariables();

            var (output, _) = RunScript("Proxy.ps1");
            var systemProxyUri = System.Net.WebRequest.GetSystemWebProxy().GetProxy(new Uri(@"http://octopus.com"));
            if (systemProxyUri.Host == "octopus.com")
            {
                output.AssertSuccess();
                output.AssertOutputContains($"HTTP_PROXY: ");
                output.AssertOutputContains($"HTTPS_PROXY: ");
            }
            else
            {
                output.AssertSuccess();
                output.AssertOutputContains($"HTTP_PROXY: http://{systemProxyUri.Host}:{systemProxyUri.Port}");
                output.AssertOutputContains($"HTTPS_PROXY: http://{systemProxyUri.Host}:{systemProxyUri.Port}");
            }
        }
#endif


        private void ResetProxyEnvironmentVariables()
        {
            EnvironmentHelper.SetEnvironmentVariable("TentacleUseDefaultProxy", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyPort", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyUsername", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyPassword", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("HTTP_PROXY", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("HTTPS_PROXY", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("NO_PROXY", string.Empty);
        }
    }
}