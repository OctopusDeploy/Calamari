using System;
using System.Collections.Generic;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
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
            var (output, _) = RunScript("print-encoded-variable.sh", new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?> { featureToggle }));

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
            var (output, _) = RunScript("print-sensitive-variable.sh", new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }));

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
            var (output, _) = RunScript("create-artifact.sh", new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("##octopus[createArtifact path='Li9zdWJkaXIvYW5vdGhlcmRpci9teWZpbGU=' name='bXlmaWxl' length='MA==']");
                            });
        }

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldUpdateProgress(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("update-progress.sh", new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
                            });
        }

        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldConsumeParametersWithQuotes(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("parameters.sh",
                                        new Dictionary<string, string>()
                                            { [SpecialVariables.Action.Script.ScriptParameters] = "\"Para meter0\" 'Para meter1'" }.AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }));

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
            var (output, _) = RunScript("parameters.sh", new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }), sensitiveVariablesPassword:
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
                                        }.AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Hello Paul");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        public void ShouldCallHelloWithSensitiveVariable(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("hello.sh",
                                        new Dictionary<string, string>()
                                            { ["Name"] = "NameToEncrypt" }.AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }), sensitiveVariablesPassword:
            "5XETGOgqYR2bRhlfhDruEg==");

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Hello NameToEncrypt");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        public void ShouldCallHelloWithNullVariable(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("hello.sh",
                                        new Dictionary<string, string>()
                                            { ["Name"] = null }.AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Hello");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        public void ShouldCallHelloWithNullSensitiveVariable(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("hello.sh",
                                        new Dictionary<string, string>()
                                            { ["Name"] = null }.AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }), sensitiveVariablesPassword:
            "5XETGOgqYR2bRhlfhDruEg==");

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("Hello");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        public void ShouldNotFailOnStdErr(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("stderr.sh",
                                        new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertErrorOutput("hello");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        public void ShouldFailOnStdErrWithTreatScriptWarningsAsErrors(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("stderr.sh",
                                        new Dictionary<string, string>()
                                            { [SpecialVariables.Action.FailScriptOnErrorOutput] = "True" }.AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }));

            Assert.Multiple(() =>
                            {
                                output.AssertFailure();
                                output.AssertErrorOutput("hello");
                            });
        }

        [RequiresBashDotExeIfOnWindows]
        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        public void ShouldNotFailOnStdErrFromServiceMessagesWithTreatScriptWarningsAsErrors(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("hello.sh",
                                        new Dictionary<string, string>()
                                            { [SpecialVariables.Action.FailScriptOnErrorOutput] = "True" }.AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }));

            output.AssertSuccess();
        }

        [RequiresBashDotExeIfOnWindows]
        [TestCase(FeatureToggle.BashParametersArrayFeatureToggle)]
        [TestCase(null)]
        public void ShouldSupportStrictVariableUnset(FeatureToggle? featureToggle)
        {
            var (output, _) = RunScript("strict-mode.sh", new Dictionary<string, string>().AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }));

            Assert.Multiple(() =>
                            {
                                output.AssertSuccess();
                                output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==']");
                            });
        }

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
                                            ["VariableName`prop`anotherprop` 13"] = "Value`prop`13"
                                        }.AddFeatureToggleToDictionary(new List<FeatureToggle?>{ featureToggle }));

            output.AssertSuccess();
            if (featureToggle == FeatureToggle.BashParametersArrayFeatureToggle)
            {
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
                output.AssertOutput("Key: VariableName \n 11, Value: Value \n 11");
                output.AssertOutput("Key: VariableName.prop.anotherprop 12, Value: Value.prop.12");
                output.AssertOutput("Key: VariableName`prop`anotherprop` 13, Value: Value`prop`13");
            }
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