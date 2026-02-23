using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldPrintEncodedVariable(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("print-encoded-variable.sh", new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?>{featureToggle}));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("##octopus[setVariable name='U3VwZXI=' value='TWFyaW8gQnJvcw==']");
                            });
        }

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldPrintSensitiveVariable(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("print-sensitive-variable.sh", new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?>{featureToggle}));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==' sensitive='VHJ1ZQ==']");
                            });
        }

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCreateArtifact(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("create-artifact.sh", new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?>{featureToggle}));

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

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldUpdateProgress(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("update-progress.sh", new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?>{featureToggle}));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
                            });
        }
        
        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldReportKubernetesManifest(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("report-kubernetes-manifest.sh", new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?>{featureToggle}));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiCiJraW5kIjogIk5hbWVzcGFjZSIKIm1ldGFkYXRhIjoKICAibmFtZSI6ICJleGFtcGxlIgoibGFiZWxzIjoKICAgICJuYW1lIjogImV4YW1wbGUiCg==']");
                                output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiCiJraW5kIjogIk5hbWVzcGFjZSIKIm1ldGFkYXRhIjoKICAibmFtZSI6ICJkaWZmcyIKImxhYmVscyI6CiAgICAibmFtZSI6ICJkaWZmcyIK']");
                                output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiCiJraW5kIjogIk5hbWVzcGFjZSIKIm1ldGFkYXRhIjoKICAibmFtZSI6ICJleGFtcGxlIgoibGFiZWxzIjoKICAgICJuYW1lIjogImV4YW1wbGUiCg==' ns='bXk=']");
                                output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiCiJraW5kIjogIk5hbWVzcGFjZSIKIm1ldGFkYXRhIjoKICAibmFtZSI6ICJkaWZmcyIKImxhYmVscyI6CiAgICAibmFtZSI6ICJkaWZmcyIK' ns='bXk=']");
                            });
        }
        
        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldReportKubernetesManifestFile(FeatureToggle? featureToggle)
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
            }.AddFeatureToggleToDictionary(new List<FeatureToggle?> { featureToggle });

            try
            {

                var (output, _) = RunScript("report-kubernetes-manifest-file.sh", additionalVariables);

                Assert.Multiple(() =>
                                {
                                    output.AssertSuccess();
                                    output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiCiJraW5kIjogIk5hbWVzcGFjZSIKIm1ldGFkYXRhIjoKICAibmFtZSI6ICJleGFtcGxlIgoibGFiZWxzIjoKICAgICJuYW1lIjogImV4YW1wbGUiCg==']");
                                    output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiCiJraW5kIjogIk5hbWVzcGFjZSIKIm1ldGFkYXRhIjoKICAibmFtZSI6ICJkaWZmcyIKImxhYmVscyI6CiAgICAibmFtZSI6ICJkaWZmcyIK']");
                                    output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiCiJraW5kIjogIk5hbWVzcGFjZSIKIm1ldGFkYXRhIjoKICAibmFtZSI6ICJleGFtcGxlIgoibGFiZWxzIjoKICAgICJuYW1lIjogImV4YW1wbGUiCg==' ns='bXk=']");
                                    output.AssertOutput("##octopus[k8s-manifest-applied manifest='ImFwaVZlcnNpb24iOiAidjEiCiJraW5kIjogIk5hbWVzcGFjZSIKIm1ldGFkYXRhIjoKICAibmFtZSI6ICJkaWZmcyIKImxhYmVscyI6CiAgICAibmFtZSI6ICJkaWZmcyIK' ns='bXk=']");
                                });
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldConsumeParametersWithQuotes(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("parameters.sh",
                                        new Dictionary<string, string>
                                            { [SpecialVariables.Action.Script.ScriptParameters] = "\"Para meter0\" 'Para meter1'" }.AddFeatureToggleToDictionary(new List<FeatureToggle?> { featureToggle }));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Parameters ($1='Para meter0' $2='Para meter1'");
                            });
        }

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldNotReceiveParametersIfNoneProvided(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("parameters.sh", new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?> { featureToggle }), sensitiveVariablesPassword:
            "5XETGOgqYR2bRhlfhDruEg==");

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Parameters ($1='' $2='')");
                            });
        }

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHello(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("hello.sh",
                                        new Dictionary<string, string>()
                                        {
                                            ["Name"] = "Paul",
                                            ["Variable2"] = "DEF",
                                            ["Variable3"] = "GHI",
                                            ["Foo_bar"] = "Hello",
                                            ["Host"] = "Never",
                                        }.AddFeatureToggleToDictionary(new List<FeatureToggle?> { featureToggle }));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Hello Paul");
                            });
        }

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHelloWithSensitiveVariable(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("hello.sh",
                                        new Dictionary<string, string>()
                                            { ["Name"] = "NameToEncrypt" }.AddFeatureToggleToDictionary(new List<FeatureToggle?> { featureToggle }), sensitiveVariablesPassword:
            "5XETGOgqYR2bRhlfhDruEg==");

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Hello NameToEncrypt");
                            });
        }

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHelloWithNullVariable(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("hello.sh",
                                        new Dictionary<string, string>()
                                            { ["Name"] = null }.AddFeatureToggleToDictionary(new List<FeatureToggle?> { featureToggle }));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Hello");
                            });
        }

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHelloWithNullSensitiveVariable(FeatureToggle? featureToggle)
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

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldNotFailOnStdErr(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("stderr.sh",
                                        new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?> { featureToggle }));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertErrorOutput("hello");
                            });
        }

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldFailOnStdErrWithTreatScriptWarningsAsErrors(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("stderr.sh",
                                        new Dictionary<string, string>()
                                            { [SpecialVariables.Action.FailScriptOnErrorOutput] = "True" }.AddFeatureToggleToDictionary(new List<FeatureToggle?> { featureToggle }));

            Assert.Multiple(() =>
                            {
                                output.AssertFailure();
                                output.AssertErrorOutput("hello");
                            });
        }

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldNotFailOnStdErrFromServiceMessagesWithTreatScriptWarningsAsErrors(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("hello.sh",
                                        new Dictionary<string, string>()
                                            { [SpecialVariables.Action.FailScriptOnErrorOutput] = "True" }.AddFeatureToggleToDictionary(new List<FeatureToggle?> { featureToggle }));

            output.AssertSuccess();
        }

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldSupportStrictVariableUnset(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("strict-mode.sh", new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?> { featureToggle }));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==']");
                            });
        }

        static string specialCharacters => "! \" # $ % & ' ( ) * + , - . / : ; < = > ? @ [ \\ ] ^ _ ` { | } ~  \n\u00b1 \u00d7 \u00f7 \u2211 \u220f \u2202 \u221e \u222b \u2248 \u2260 \u2264 \u2265 \u221a \u221b \u2206 \u2207 \u221d  \n$ \u00a2 \u00a3 \u00a5 \u20ac \u20b9 \u20a9 \u20b1 \u20aa \u20bf  \n• ‣ … ′ ″ ‘ ’ “ ” ‽ ¡ ¿ – — ―  \n( ) [ ] { } ⟨ ⟩ « » ‘ ’ “ ”  \n\u2190 \u2191 \u2192 \u2193 \u2194 \u2195 \u2196 \u2197 \u2198 \u2199 \u2b05 \u2b06 \u2b07 \u27a1 \u27f3  \nα β γ δ ε ζ η θ ι κ λ μ ν ξ ο π ρ σ τ υ φ χ ψ ω  \n\u00a9 \u00ae \u2122 § ¶ † ‡ µ #";

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
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
                                            [specialCharacters] = specialCharacters,
                                            // Emoji / 4-byte UTF-8 codepoints
                                            ["EmojiKey 🎉💡🔥"] = "EmojiValue 😀🌍🚀",
                                            // CJK (Chinese / Japanese / Korean)
                                            ["CJK 中文 日本語 한국어"] = "中文值 你好世界",
                                            // Arabic RTL text
                                            ["Arabic مفتاح"] = "قيمة عربية",
                                            // Bash command-injection attempts in both key and value
                                            ["InjectionAttempt $(echo injected)"] = "$(echo injected) `echo injected` ${HOME}",
                                            // Empty value
                                            ["EmptyValueKey"] = "",
                                            // Leading and trailing whitespace in value
                                            ["LeadingTrailingSpaces"] = "  value padded with spaces  ",
                                            // Multiple '=' signs in value (parsers that split on '=' can mis-handle this)
                                            ["MultipleEquals"] = "a=b=c=d",
                                            // Zero-width space (U+200B) – invisible but load-bearing
                                            ["ZeroWidth\u200bKey"] = "zero\u200bwidth\u200bvalue",
                                            // ANSI escape sequence – terminal control injection attempt
                                            ["AnsiEscapeKey"] = "\x1b[31mRed\x1b[0m",
                                            // Supplementary-plane Unicode (mathematical script + musical symbols)
                                            ["SupplementaryPlane 𝒜𝄞"] = "value 𝐀𝁆",
                                            // Combining diacritical mark (NFD 'é' vs NFC U+00E9)
                                            ["CombiningDiacritical Caf\u0301"] = "Caf\u00e9",
                                        }.AddFeatureToggleToDictionary(new List<FeatureToggle?> { FeatureToggle.BashParametersArrayFeatureToggle }));

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

            output.AssertOutput("Key: EmojiKey 🎉💡🔥, Value: EmojiValue 😀🌍🚀");
            output.AssertOutput("Key: CJK 中文 日本語 한국어, Value: 中文值 你好世界");
            output.AssertOutput("Key: Arabic مفتاح, Value: قيمة عربية");
            output.AssertOutput("Key: InjectionAttempt $(echo injected), Value: $(echo injected) `echo injected` ${HOME}");
            output.AssertOutput("Key: EmptyValueKey, Value: ");
            output.AssertOutput("Key: LeadingTrailingSpaces, Value:   value padded with spaces  ");
            output.AssertOutput("Key: MultipleEquals, Value: a=b=c=d");
            output.AssertOutput($"Key: ZeroWidth\u200bKey, Value: zero\u200bwidth\u200bvalue");
            output.AssertOutput($"Key: AnsiEscapeKey, Value: \x1b[31mRed\x1b[0m");
            output.AssertOutput($"Key: SupplementaryPlane 𝒜𝄞, Value: value 𝐀𝁆");
            output.AssertOutput($"Key: CombiningDiacritical Caf\u0301, Value: Caf\u00e9");
        }

        [Explicit]
        [Category("Performance")]
        [TestCase(  100,  30_000, Description = "100 variables (~small deployment)")]
        [TestCase(  500,  60_000, Description = "500 variables (~medium deployment)")]
        [TestCase(1_000, 120_000, Description = "1 000 variables (~large deployment)")]
        [TestCase(5_000, 300_000, Description = "5 000 variables (~stress test)")]
        [TestCase(20_000, 300_000, Description = "5 000 variables (~stress test)")]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldPopulateOctopusParametersPerformantly(int variableCount, int timeLimitMs)
        {
            // Build the realistic payload separately so we can measure its size
            // before it gets encrypted and written into the bootstrap script.
            var perfVariables = BuildPerformanceTestVariables(variableCount);
            var variables = new Dictionary<string, string>(perfVariables)
                                .AddFeatureToggleToDictionary(new List<FeatureToggle?> { FeatureToggle.BashParametersArrayFeatureToggle });

            var keyBytes   = perfVariables.Keys  .Select(k => (long)Encoding.UTF8.GetByteCount(k))      .ToArray();
            var valueBytes = perfVariables.Values.Select(v => (long)Encoding.UTF8.GetByteCount(v ?? "")).ToArray();
            var totalKeyBytes   = keyBytes.Sum();
            var totalValueBytes = valueBytes.Sum();
            var totalPairBytes  = totalKeyBytes + totalValueBytes;

            var stopwatch = Stopwatch.StartNew();
            var (output, _) = RunScript("count-octopus-parameters.sh", variables);
            stopwatch.Stop();

            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // ── Structured performance report ────────────────────────────────────
            static string Kb(long b)    => $"{b / 1024.0,7:F1} KB";
            static string Fmt(double b) => b >= 1024 ? $"{b / 1024.0:F1} KB" : $"{b:F0} B";

            TestContext.WriteLine("");
            TestContext.WriteLine("── Payload ─────────────────────────────────────────────────────");
            TestContext.WriteLine($"  Variables  : {perfVariables.Count,6:N0}");
            TestContext.WriteLine($"  Keys       :  avg {Fmt(keyBytes.Average()),9}  │  min {keyBytes.Min(),5} B  │  max {keyBytes.Max(),7} B  │  total {Kb(totalKeyBytes)}");
            TestContext.WriteLine($"  Values     :  avg {Fmt(valueBytes.Average()),9}  │  min {valueBytes.Min(),5} B  │  max {valueBytes.Max(),7} B  │  total {Kb(totalValueBytes)}");
            TestContext.WriteLine($"  Pairs      :  avg {Fmt(keyBytes.Average() + valueBytes.Average()),9}  │                              │  total {Kb(totalPairBytes)}");
            TestContext.WriteLine("── Timing ──────────────────────────────────────────────────────");
            TestContext.WriteLine($"  Total      : {elapsedMs,7} ms  (limit {timeLimitMs / 1000} s)");
            TestContext.WriteLine($"  Per var    : {elapsedMs / (double)perfVariables.Count,9:F2} ms");
            TestContext.WriteLine($"  Throughput : {totalPairBytes / 1024.0 / (elapsedMs / 1000.0),7:F1} KB/s");
            TestContext.WriteLine("────────────────────────────────────────────────────────────────");

            output.AssertSuccess();

            var fullOutput = string.Join(Environment.NewLine, output.CapturedOutput.Infos);
            if (fullOutput.Contains("Bash version 4.2 or later is required to use octopus_parameters"))
            {
                Assert.Ignore("Bash 4.2+ required for octopus_parameters; performance assertion skipped.");
                return;
            }

            var countLine = output.GetOutputForLineContaining("VariableCount=");
            var loadedCount = int.Parse(Regex.Match(countLine, @"VariableCount=(\d+)").Groups[1].Value);
            loadedCount.Should().BeGreaterThanOrEqualTo(
                variableCount,
                $"octopus_parameters should contain at least the {variableCount} variables we passed in");

            output.AssertOutput("SpotCheck=PerfSentinelValue");

            elapsedMs.Should().BeLessThan(
                timeLimitMs,
                $"Loading {variableCount} variables should complete within {timeLimitMs / 1000.0:F0}s");
        }

        // ---------------------------------------------------------------------------
        // Realistic variable generator for performance tests.
        //
        // Produces a distribution that approximates a real Octopus deployment:
        //
        //   Bucket  | Share | Name length  | Value length   | Example
        //   --------|-------|--------------|----------------|----------------------------
        //   Small   |  60%  |  15–30 chars |   10–40 chars  | Project.Name, port numbers
        //   Medium  |  25%  |  40–65 chars | 150–300 chars  | SQL / Redis connection strings
        //   Large   |  10%  |  70–130 chars|  700–950 chars | JSON config blobs
        //   Huge    |   5%  | 100–180 chars| 6 000–9 000 chars | PEM certificate + key bundles
        //
        // All keys are unique (every template appends the loop index).
        // A "PerfSentinel" key is added for correctness spot-checks.
        // ---------------------------------------------------------------------------

        static Dictionary<string, string> BuildPerformanceTestVariables(int count)
        {
            var result = new Dictionary<string, string>(count + 1);
            for (var i = 0; i < count; i++)
            {
                var (key, value) = (i % 20) switch
                {
                    < 12 => (PerfSmallKey(i),  PerfSmallValue(i)),
                    < 17 => (PerfMediumKey(i), PerfMediumValue(i)),
                    < 19 => (PerfLargeKey(i),  PerfLargeValue(i)),
                    _    => (PerfHugeKey(i),   PerfHugeValue(i)),
                };
                result[key] = value;
            }
            result["PerfSentinel"] = "PerfSentinelValue";
            return result;
        }

        // Small (60%): names ~15–30 chars, values ~10–40 chars
        static string PerfSmallKey(int i) => (i % 12) switch
        {
            0  => $"Project.Name.{i:D4}",
            1  => $"Environment.Region.{i:D4}",
            2  => $"Application.Version.{i:D4}",
            3  => $"Config.Timeout.Seconds.{i:D4}",
            4  => $"Deploy.Mode.{i:D4}",
            5  => $"Service.Port.{i:D4}",
            6  => $"Feature.{i:D4}.Enabled",
            7  => $"Build.Number.{i:D4}",
            8  => $"Cluster.Zone.{i:D4}",
            9  => $"Agent.Pool.{i:D4}",
            10 => $"Release.Channel.{i:D4}",
            _  => $"Tag.Value.{i:D4}",
        };

        static string PerfSmallValue(int i) => (i % 12) switch
        {
            0  => $"production-{i % 5}",
            1  => $"us-{(i % 2 == 0 ? "east" : "west")}-{i % 3 + 1}",
            2  => $"v{i % 10}.{i % 100 + 1}.{i % 1000}",
            3  => $"{i % 120 + 10}",
            4  => i % 2 == 0 ? "blue-green" : "rolling",
            5  => $"{8080 + i % 100}",
            6  => i % 2 == 0 ? "true" : "false",
            7  => $"build-{i:D6}",
            8  => $"zone-{(char)('a' + i % 3)}",
            9  => $"pool-{i % 4}",
            10 => i % 2 == 0 ? "stable" : "beta",
            _  => $"tag-{i % 20:D3}",
        };

        // Medium (25%): names ~40–65 chars, values ~150–300 chars
        static string PerfMediumKey(int i) => (i % 5) switch
        {
            0 => $"Application.Database.Primary.ConnectionString.{i:D4}",
            1 => $"Azure.Storage.Account{i % 3}.AccessKey.{i:D4}",
            2 => $"Service.Authentication.BearerToken.Endpoint.{i:D4}",
            3 => $"Infrastructure.Cache.Redis{i % 2}.ConnectionString.{i:D4}",
            _ => $"Octopus.Action.Package[MyApp.Service{i % 5}].FeedUri.{i:D4}",
        };

        static string PerfMediumValue(int i) => (i % 5) switch
        {
            0 => $"Server=tcp:sql{i % 3}.database.windows.net,1433;Initial Catalog=AppDb{i % 10};User Id=svc_app;Password=P@ssw0rd{i:D4}!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
            1 => $"DefaultEndpointsProtocol=https;AccountName=storage{i % 5}acct;AccountKey=FAKE-KEY-NOT-A-SECRET-{i:D4};EndpointSuffix=core.windows.net",
            2 => $"https://login.microsoftonline.com/{i:D8}-aaaa-bbbb-cccc-{i:D12}/oauth2/v2.0/token",
            3 => $"cache{i % 2}.redis.cache.windows.net:6380,password=cacheSecret{i:D4}==,ssl=true,abortConnect=false,connectTimeout=5000,syncTimeout=3000",
            _ => $"https://nuget.pkg.github.com/MyOrganisation{i % 3}/index.json?api-version=6.0",
        };

        // Large (10%): names ~70–130 chars, values ~700–950 chars (JSON config blobs)
        static string PerfLargeKey(int i) =>
            $"Octopus.Action[Step {i % 10}: Deploy {(i % 2 == 0 ? "Application" : "Infrastructure")} to {(i % 3 == 0 ? "Production" : "Staging")}].Package[MyCompany.Service{i % 5}].Config.{i:D4}";

        static string PerfLargeValue(int i) => $@"{{
  ""environment"": ""{(i % 2 == 0 ? "production" : "staging")}"",
  ""instanceId"": ""{i:D8}"",
  ""serviceEndpoints"": {{
    ""auth"":   ""https://auth{i % 3}.internal.company.com/oauth/token"",
    ""users"":  ""https://users{i % 3}.internal.company.com/api/v2"",
    ""events"": ""https://events{i % 3}.internal.company.com/stream""
  }},
  ""database"": {{
    ""primary"": ""Server=tcp:primary{i % 2}.db.internal,1433;Initial Catalog=AppDb;User ID=svc_app;Password=P@ssw0rd{i:D6}!;Encrypt=True;Connection Timeout=30;"",
    ""replica"":  ""Server=tcp:replica{i % 3}.db.internal,1433;Initial Catalog=AppDb;User ID=svc_ro;Password=R3adOnly{i:D6}!;Encrypt=True;Connection Timeout=30;""
  }},
  ""cache"": {{
    ""primary"":   ""cache{i % 2}.redis.cache.windows.net:6380,password=cacheP@ss{i:D4}==,ssl=true"",
    ""secondary"": ""cache{(i + 1) % 2}.redis.cache.windows.net:6380,password=cacheP@ss{i:D4}==,ssl=true""
  }},
  ""storage"": {{
    ""accountName"":   ""storage{i % 5}acct"",
    ""containerName"": ""deployments-{i:D4}"",
    ""sasToken"":      ""sv=2021-12-02&ss=b&srt=co&sp=rwdlacupiytfx&se=2025-12-31T23:59:59Z&st=2024-01-01T00:00:00Z&spr=https&sig=fakeSignature{i:D6}==""
  }}
}}";

        // Huge (5%): names ~100–180 chars, values ~6 000–9 000 chars (PEM cert + private key bundle)
        static string PerfHugeKey(int i) =>
            $"Octopus.Action[Step {i % 5}: Long Running {(i % 2 == 0 ? "Database Migration" : "Certificate Rotation")} Task For {(i % 3 == 0 ? "Production-AUS" : "Production-US")} Environment].Package[MyCompany.{(i % 2 == 0 ? "Infrastructure" : "Security")}.Tooling.v{i % 10}].Config.{i:D4}";

        static string PerfHugeValue(int i)
        {
            // Each cert line is 60 chars of base64 + 8-digit serial + "==" + newline ≈ 72 chars.
            // 60 + 52 + 42 lines across 3 PEM blocks ≈ 154 lines × 72 chars ≈ 11 KB per variable.
            const string b64 = "MIIGXTCCBEWgAwIBAgIJAKnmpBuMNbOBMA0GCSqGSIb3DQEBCwUAMIGaMQswCQYD"; // 64 chars
            var sb = new StringBuilder(9000);
            sb.AppendLine($"# Certificate bundle — slot {i % 3}, serial {i:D8}");
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            for (var line = 0; line < 60; line++)
                sb.AppendLine($"{b64.Substring((i + line) % 4, 60)}{i * 31337 + line * 7:D8}==");
            sb.AppendLine("-----END CERTIFICATE-----");
            sb.AppendLine("-----BEGIN FAKE TEST KEY-----");
            for (var line = 0; line < 52; line++)
                sb.AppendLine($"{b64.Substring((i + line + 2) % 4, 60)}{i * 99991 + line * 13:D8}Ag==");
            sb.AppendLine("-----END FAKE TEST KEY-----");
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            for (var line = 0; line < 42; line++)
                sb.AppendLine($"{b64.Substring((i + line + 1) % 4, 60)}{i * 65537 + line * 11:D8}==");
            sb.AppendLine("-----END CERTIFICATE-----");
            return sb.ToString();
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