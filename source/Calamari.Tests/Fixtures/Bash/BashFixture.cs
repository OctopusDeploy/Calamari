using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Common.Features.Processes;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Bash
{
    [TestFixture]
    public class BashFixture : CalamariFixture
    {
        [RequiresBashDotExeIfOnWindows]
        public void ShouldPrintEncodedVariable()
        {
            var (output, _) = RunScript("print-encoded-variable.sh", new Dictionary<string, string>());

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("##octopus[setVariable name='U3VwZXI=' value='TWFyaW8gQnJvcw==']");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        public void ShouldPrintSensitiveVariable()
        {
            var (output, _) = RunScript("print-sensitive-variable.sh");

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==' sensitive='VHJ1ZQ==']");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        public void ShouldCreateArtifact()
        {
            var (output, _) = RunScript("create-artifact.sh");

            const string regexPattern = @"##octopus\[createArtifact path='([\S]+)' name='bXlmaWxl' length='MA==']";
    
            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutputMatches(regexPattern);

                                
                                var match = Regex.Match(output.CapturedOutput.ToString(), regexPattern);
                                match.Success.Should().BeTrue();
                                
                                //the second match is the first match group
                                var matchedPath = match.Groups[1];
                                
                                //decoded path
                                var decodedPath = Encoding.UTF8.GetString(Convert.FromBase64String(matchedPath.Value));

                                //even on windows, this runs in linux (WSL), so the path will always be this direction
                                decodedPath.Should().EndWith($"/subdir/anotherdir/myfile");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        public void ShouldUpdateProgress()
        {
            var (output, _) = RunScript("update-progress.sh");

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
                            });
        }
        
        [RequiresBashDotExeIfOnWindows]
        public void ShouldReportKubernetesManifest()
        {
            var (output, _) = RunScript("report-kubernetes-manifest.sh");

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiXG4ia2luZCI6ICJOYW1lc3BhY2UiXG4ibWV0YWRhdGEiOlxuICAibmFtZSI6ICJleGFtcGxlIlxuImxhYmVscyI6XG4gICAgIm5hbWUiOiAiZXhhbXBsZSJcbg==']");
                                output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiXG4ia2luZCI6ICJOYW1lc3BhY2UiXG4ibWV0YWRhdGEiOlxuICAibmFtZSI6ICJkaWZmcyJcbiJsYWJlbHMiOlxuICAgICJuYW1lIjogImRpZmZzIlxu']");
                                output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiXG4ia2luZCI6ICJOYW1lc3BhY2UiXG4ibWV0YWRhdGEiOlxuICAibmFtZSI6ICJleGFtcGxlIlxuImxhYmVscyI6XG4gICAgIm5hbWUiOiAiZXhhbXBsZSJcbg==' ns='bXk=']");
                                output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiXG4ia2luZCI6ICJOYW1lc3BhY2UiXG4ibWV0YWRhdGEiOlxuICAibmFtZSI6ICJkaWZmcyJcbiJsYWJlbHMiOlxuICAgICJuYW1lIjogImRpZmZzIlxu' ns='bXk=']");
                            });
        }
        
        [RequiresBashDotExeIfOnWindows]
        public void ShouldReportKubernetesManifestFile()
        {
            var tempPath = Path.GetTempPath();
            var manifest = @"""apiVersion"": ""v1""
""kind"": ""Namespace""
""metadata"":
  ""name"": ""example""
""labels"":
    ""name"": ""example""
---    
""apiVersion"": ""v1""
""kind"": ""Namespace""
""metadata"":
  ""name"": ""diffs""
""labels"":
    ""name"": ""diffs""".ReplaceLineEndings("\n");
            
            var filePath = Path.Combine(tempPath, "ShouldWriteServiceMessageForKubernetesManifestFile.manifest.yaml");
            File.WriteAllText(filePath, manifest);

            //if we are running on windows, we must be running via bash.exe, so we need to translate this to a wsl path
            var updatedFilePath = filePath;
            if (CalamariEnvironment.IsRunningOnWindows)
            {
                var qualifiedPath = filePath.Replace(@"\",@"\\");

                var path = string.Empty;
                var result = SilentProcessRunner.ExecuteCommand("wsl", $"wslpath -a -u {qualifiedPath}", tempPath, output => path = output,
                                                                _ => { });
                
                if (result.ExitCode != 0)
                {
                    Assert.Fail("Failed to convert windows path to WSL path");
                    return;
                }

                updatedFilePath = path;
            }

            var additionalVariables = new Dictionary<string, string>
            {
                { "ManifestFilePath", updatedFilePath }
            };

            try
            {

                var (output, _) = RunScript("report-kubernetes-manifest-file.sh", additionalVariables);

                Assert.Multiple(() =>
                                {
                                    output.AssertSuccess();
                                    output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiXG4ia2luZCI6ICJOYW1lc3BhY2UiXG4ibWV0YWRhdGEiOlxuICAibmFtZSI6ICJleGFtcGxlIlxuImxhYmVscyI6XG4gICAgIm5hbWUiOiAiZXhhbXBsZSJcbg==']");
                                    output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiXG4ia2luZCI6ICJOYW1lc3BhY2UiXG4ibWV0YWRhdGEiOlxuICAibmFtZSI6ICJkaWZmcyJcbiJsYWJlbHMiOlxuICAgICJuYW1lIjogImRpZmZzIlxu']");
                                    output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiXG4ia2luZCI6ICJOYW1lc3BhY2UiXG4ibWV0YWRhdGEiOlxuICAibmFtZSI6ICJleGFtcGxlIlxuImxhYmVscyI6XG4gICAgIm5hbWUiOiAiZXhhbXBsZSJcbg==' ns='bXk=']");
                                    output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiXG4ia2luZCI6ICJOYW1lc3BhY2UiXG4ibWV0YWRhdGEiOlxuICAibmFtZSI6ICJkaWZmcyJcbiJsYWJlbHMiOlxuICAgICJuYW1lIjogImRpZmZzIlxu' ns='bXk=']");
                                });
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [RequiresBashDotExeIfOnWindows]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunScript("parameters.sh",
                                        new Dictionary<string, string>()
                                            { [SpecialVariables.Action.Script.ScriptParameters] = "\"Para meter0\" 'Para meter1'" });

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Parameters ($1='Para meter0' $2='Para meter1'");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        public void ShouldNotReceiveParametersIfNoneProvided()
        {
            var (output, _) = RunScript("parameters.sh", new Dictionary<string, string>(), sensitiveVariablesPassword:
            "5XETGOgqYR2bRhlfhDruEg==");

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Parameters ($1='' $2='')");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHello()
        {
            var (output, _) = RunScript("hello.sh",
                                        new Dictionary<string, string>()
                                        {
                                            ["Name"] = "Paul",
                                            ["Variable2"] = "DEF",
                                            ["Variable3"] = "GHI",
                                            ["Foo_bar"] = "Hello",
                                            ["Host"] = "Never",
                                        });

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Hello Paul");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var (output, _) = RunScript("hello.sh",
                                        new Dictionary<string, string>()
                                            { ["Name"] = "NameToEncrypt" }, sensitiveVariablesPassword:
            "5XETGOgqYR2bRhlfhDruEg==");

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Hello NameToEncrypt");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHelloWithNullVariable()
        {
            var (output, _) = RunScript("hello.sh",
                                        new Dictionary<string, string>()
                                            { ["Name"] = null });

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Hello");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHelloWithNullSensitiveVariable()
        {
            var (output, _) = RunScript("hello.sh",
                                        new Dictionary<string, string>()
                                            { ["Name"] = null }, sensitiveVariablesPassword:
            "5XETGOgqYR2bRhlfhDruEg==");

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Hello");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        public void ShouldNotFailOnStdErr()
        {
            var (output, _) = RunScript("stderr.sh",
                                        new Dictionary<string, string>());

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertErrorOutput("hello");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        public void ShouldFailOnStdErrWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunScript("stderr.sh",
                                        new Dictionary<string, string>()
                                            { [SpecialVariables.Action.FailScriptOnErrorOutput] = "True" });

            Assert.Multiple(() =>
                            {
                                output.AssertFailure();
                                output.AssertErrorOutput("hello");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        public void ShouldNotFailOnStdErrFromServiceMessagesWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunScript("hello.sh",
                                        new Dictionary<string, string>()
                                            { [SpecialVariables.Action.FailScriptOnErrorOutput] = "True" });

            output.AssertSuccess();
        }

        [RequiresBashDotExeIfOnWindows]
        public void ShouldSupportStrictVariableUnset()
        {
            var (output, _) = RunScript("strict-mode.sh", new Dictionary<string, string>());

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==']");
                            });
        }

        static string specialCharacters => "! \" # $ % & ' ( ) * + , - . / : ; < = > ? @ [ \\ ] ^ _ ` { | } ~  \n\u00b1 \u00d7 \u00f7 \u2211 \u220f \u2202 \u221e \u222b \u2248 \u2260 \u2264 \u2265 \u221a \u221b \u2206 \u2207 \u221d  \n$ \u00a2 \u00a3 \u00a5 \u20ac \u20b9 \u20a9 \u20b1 \u20aa \u20bf  \n• ‣ … ′ ″ ‘ ’ “ ” ‽ ¡ ¿ – — ―  \n( ) [ ] { } ⟨ ⟩ « » ‘ ’ “ ”  \n\u2190 \u2191 \u2192 \u2193 \u2194 \u2195 \u2196 \u2197 \u2198 \u2199 \u2b05 \u2b06 \u2b07 \u27a1 \u27f3  \nα β γ δ ε ζ η θ ι κ λ μ ν ξ ο π ρ σ τ υ φ χ ψ ω  \n\u00a9 \u00ae \u2122 § ¶ † ‡ µ #";

        [RequiresBashDotExeIfOnWindows]
        public void ShouldBeAbleToEnumerateVariableValues(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("enumerate-variables.sh",
                                        new Dictionary<string, string>()
                                        {
                                            ["VariableName1"] = "Value 1",
                                            ["VariableName 2"] = "Value 2",
                                            ["VariableName3"] = "Value 3",
                                            ["VariableName '4'"] = "Value '4'",
                                            ["VariableName \"5\""] = "Value \"5\"",
                                            ["VariableName, 6"] = "Value, 6",
                                            ["VariableName [7]"] = "Value [7]",
                                            ["VariableName {8}"] = "Value {8}",
                                            ["VariableName\t9"] = "Value\t9",
                                            ["VariableName 10 !@#$%^&*()_+1234567890-="] = "Value 10 !@#$%^&*()_+1234567890-=",
                                            ["VariableName \n 11"] = "Value \n 11",
                                            ["VariableName.prop.anotherprop 12"] = "Value.prop.12",
                                            ["VariableName`prop`anotherprop` 13"] = "Value`prop`13",
                                            [specialCharacters] = specialCharacters
                                        });

            output.AssertSuccess();
            var fullOutput = string.Join(Environment.NewLine, output.CapturedOutput.Infos);
            if (fullOutput.Contains("Bash version 4.2 or later is required to use octopus_parameters"))
            {
                output.AssertOutput("Still ran this script");
                return;
            }

            output.AssertOutput(@"Key: VariableName1, Value: Value 1");
            output.AssertOutput(@"Key: VariableName 2, Value: Value 2");
            output.AssertOutput(@"Key: VariableName3, Value: Value 3");
            output.AssertOutput(@"Key: VariableName '4', Value: Value '4'");
            output.AssertOutput("Key: VariableName \"5\", Value: Value \"5\"");
            output.AssertOutput(@"Key: VariableName, 6, Value: Value, 6");
            output.AssertOutput(@"Key: VariableName [7], Value: Value [7]");
            output.AssertOutput(@"Key: VariableName {8}, Value: Value {8}");
            output.AssertOutput("Key: VariableName\t9, Value: Value\t9");
            output.AssertOutput(@"Key: VariableName 10 !@#$%^&*()_+1234567890-=, Value: Value 10 !@#$%^&*()_+1234567890-=");
            if (CalamariEnvironment.IsRunningOnNix)
            {
                output.AssertOutput("Key: VariableName \n 11, Value: Value \n 11");
            }

            output.AssertOutput("Key: VariableName.prop.anotherprop 12, Value: Value.prop.12");
            output.AssertOutput("Key: VariableName`prop`anotherprop` 13, Value: Value`prop`13");
            output.AssertOutput($"Key: {specialCharacters}, Value: {specialCharacters}");
        }
    }

    public static class AdditionalVariablesExtensions
    {
        public static Dictionary<string, string> AddFeatureToggleToDictionary(this Dictionary<string, string> variables, List<FeatureToggle?> featureToggles)
        {
            variables.Add(KnownVariables.EnabledFeatureToggles, string.Join(", ", featureToggles));
            return variables;
        }
    }
}
