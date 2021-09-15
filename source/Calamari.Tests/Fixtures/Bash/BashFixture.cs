using System;
using System.Collections.Generic;
using Calamari.Deployment;
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

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='U3VwZXI=' value='TWFyaW8gQnJvcw==']");
        }
        
        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldPrintSensitiveVariable()
        {
            var (output, _) = RunScript("print-sensitive-variable.sh");

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==' sensitive='VHJ1ZQ==']");
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCreateArtifact()
        {
            var (output, _) = RunScript("create-artifact.sh");

            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact path='Li9zdWJkaXIvYW5vdGhlcmRpci9teWZpbGU=' name='bXlmaWxl' length='MA==']");
        }
        
        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldUpdateProgress()
        {
            var (output, _) = RunScript("update-progress.sh");

            output.AssertSuccess();
            output.AssertOutput("##octopus[progress percentage='NTA=' message='SGFsZiBXYXk=']");
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunScript("parameters.sh", new Dictionary<string, string>()
                { [SpecialVariables.Action.Script.ScriptParameters] = "\"Para meter0\" 'Para meter1'" });

            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Para meter1");
        }
        
        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldNotHaveDecryptionKeyInScopeOfUserScript()
        {
            var (output, _) = RunScript("parameters.sh", new Dictionary<string, string>()
                                            { ["Name"] = "NameToEncrypt", [SpecialVariables.Action.Script.ScriptParameters] = "" }, 
                                            sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            output.AssertSuccess();
            output.AssertOutput("Parameters ");
            output.AssertNoOutputMatches(@"Parameters ([A-Z0-9])+");
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

            output.AssertSuccess();
            output.AssertOutput("Hello Paul");
        }


        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
                { ["Name"] = "NameToEncrypt" }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

                output.AssertSuccess();
                output.AssertOutput("Hello NameToEncrypt");
        }


        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHelloWithNullVariable()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
                {["Name"] = null});

            output.AssertSuccess();
            output.AssertOutput("Hello");
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldCallHelloWithNullSensitiveVariable()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
                { ["Name"] = null }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            output.AssertSuccess();
            output.AssertOutput("Hello");
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldNotFailOnStdErr()
        {
            var (output, _) = RunScript("stderr.sh");

            output.AssertSuccess();
            output.AssertErrorOutput("hello");
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldFailOnStdErrWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunScript("stderr.sh", new Dictionary<string, string>()
                {[SpecialVariables.Action.FailScriptOnErrorOutput] = "True"});

            output.AssertFailure();
            output.AssertErrorOutput("hello");
        }

        [Test]
        [RequiresBashDotExeIfOnWindows]
        public void ShouldSupportStrictVariableUnset()
        {
            var (output, _) = RunScript("strict-mode.sh");

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==']");
        }
    }
}