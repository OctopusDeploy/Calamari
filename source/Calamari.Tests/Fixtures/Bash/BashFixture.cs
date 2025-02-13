using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Bash
{
    [TestFixture]
    public class BashFixture : CalamariFixture
    {
        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldPrintEncodedVariable()
        {
            var (output, _) = RunScript("print-encoded-variable.sh");

            Assert.Multiple(() => {
                output.AssertSuccess();
                output.AssertOutput("##octopus[setVariable name='U3VwZXI=' value='TWFyaW8gQnJvcw==']");
            });
        }
        
        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldPrintSensitiveVariable()
        {
            var (output, _) = RunScript("print-sensitive-variable.sh");

            Assert.Multiple(() => {
                output.AssertSuccess();
                output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==' sensitive='VHJ1ZQ==']");
            });
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCreateArtifact()
        {
            var (output, _) = RunScript("create-artifact.sh");

            Assert.Multiple(() => {
                output.AssertSuccess();
                output.AssertOutput("##octopus[createArtifact path='Li9zdWJkaXIvYW5vdGhlcmRpci9teWZpbGU=' name='bXlmaWxl' length='MA==']");
            });
        }
        
        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldUpdateProgress()
        {
            var (output, _) = RunScript("update-progress.sh");

            Assert.Multiple(() => {
                output.AssertSuccess();
                output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
            });
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunScript("parameters.sh", new Dictionary<string, string>()
                { [SpecialVariables.Action.Script.ScriptParameters] = "\"Para meter0\" 'Para meter1'" });

            Assert.Multiple(() => {
                output.AssertSuccess();
                output.AssertOutput("Parameters ($1='Para meter0' $2='Para meter1'");
            });
        }
        
        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldNotReceiveParametersIfNoneProvided()
        {
            var (output, _) = RunScript("parameters.sh", sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            Assert.Multiple(() => {
                output.AssertSuccess();
                output.AssertOutput("Parameters ($1='' $2='')");
            });
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHello()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
            {
                ["Name"] = "Paul",
                ["Variable2"] = "DEF",
                ["Variable3"] = "GHI",
                ["Foo_bar"] = "Hello",
                ["Host"] = "Never",
            });

            Assert.Multiple(() => {
                output.AssertSuccess();
                output.AssertOutput("Hello Paul");
            });
        }


        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
                { ["Name"] = "NameToEncrypt" }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            Assert.Multiple(() => {
                output.AssertSuccess();
                output.AssertOutput("Hello NameToEncrypt");
            });
        }


        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHelloWithNullVariable()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
                {["Name"] = null});

            Assert.Multiple(() => {
                output.AssertSuccess();
                output.AssertOutput("Hello");
            });
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHelloWithNullSensitiveVariable()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
                { ["Name"] = null }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            Assert.Multiple(() => {
                output.AssertSuccess();
                output.AssertOutput("Hello");
            });
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldNotFailOnStdErr()
        {
            var (output, _) = RunScript("stderr.sh");
            
            Assert.Multiple(() => {
                output.AssertSuccess();
                output.AssertErrorOutput("hello");
            });
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldFailOnStdErrWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunScript("stderr.sh", new Dictionary<string, string>()
                {[SpecialVariables.Action.FailScriptOnErrorOutput] = "True"});

            Assert.Multiple(() => {
                output.AssertFailure();
                output.AssertErrorOutput("hello");
            });
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldNotFailOnStdErrFromServiceMessagesWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
            {[SpecialVariables.Action.FailScriptOnErrorOutput] = "True"});

            output.AssertSuccess();
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldSupportStrictVariableUnset()
        {
            var (output, _) = RunScript("strict-mode.sh");

            Assert.Multiple(() => {
                output.AssertSuccess();
                output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==']");
            });
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldBeAbleToEnumerateVariableValues()
        {
            var (output, _) = RunScript("enumerate-variables.sh", new Dictionary<string, string>()
            {
                ["VariableName1"] = "Value 1",
                ["VariableName 2"] = "Value 2",
                ["VariableName3"] = "Value 3",
                ["VariableName '4'"] = "Value '4'",
                ["VariableName \"5\""] = "Value \"5\"",
                ["VariableName, 6"] = "Value, 6",
                ["VariableName, [7]"] = "Value [7]",
                ["VariableName, {8}"] = "Value {8}",
                ["VariableName\t9"] = "Value\t9",
                ["VariableName 10 !@#$%^&*()_+1234567890-="] = "Value 10 !@#$%^&*()_+1234567890-=",
                ["VariableName, \n 11"] = "Value \n 11",
            });

            output.AssertSuccess();
            output.AssertOutput(@"Key: VariableName1, Value: Value 1");
            output.AssertOutput(@"Key: VariableName 2, Value: Value 2");
            output.AssertOutput(@"Key: VariableName3, Value: Value 3");
            output.AssertOutput(@"Key: VariableName '4', Value: Value '4'");
            output.AssertOutput("Key: VariableName \"5\", Value: Value \"5\"");
            output.AssertOutput(@"Key: VariableName, 6, Value: Value, 6");
            output.AssertOutput(@"Key: VariableName [7], Value: Value [7]");
            output.AssertOutput(@"Key: VariableName {8}, Value: Value {8}");
            output.AssertOutput(@"Key: VariableName\t9, Value: Value\t9");
            output.AssertOutput(@"Key: VariableName 10 !@#$%^&*()_+1234567890-=, Value: Value 10 !@#$%^&*()_+1234567890-=");
            output.AssertOutput("Key: VariableName \n 11, Value: Value \n 11");
        }
    }
}