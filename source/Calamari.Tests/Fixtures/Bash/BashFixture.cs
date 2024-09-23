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
    }
}