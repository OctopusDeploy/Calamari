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
        [Category(TestCategory.CompatibleOS.Nix)]
        [Category(TestCategory.CompatibleOS.Mac)]
        public void ShouldPrintEncodedVariable()
        {
            var (output, _) = RunScript("print-encoded-variable.sh");

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='U3VwZXI=' value='TWFyaW8gQnJvcw==']");
        }
        
        [Test]
        [Category(TestCategory.CompatibleOS.Nix)]
        [Category(TestCategory.CompatibleOS.Mac)]
        public void ShouldPrintSensitiveVariable()
        {
            var (output, _) = RunScript("print-sensitive-variable.sh");

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==' sensitive='VHJ1ZQ==']");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Nix)]
        [Category(TestCategory.CompatibleOS.Mac)]
        public void ShouldCreateArtifact()
        {
            var (output, _) = RunScript("create-artifact.sh");

            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact path='Li9zdWJkaXIvYW5vdGhlcmRpci9teWZpbGU=' name='bXlmaWxl' length='MA==']");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Nix)]
        [Category(TestCategory.CompatibleOS.Mac)]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunScript("parameters.sh", new Dictionary<string, string>()
                { [SpecialVariables.Action.Script.ScriptParameters] = "\"Para meter0\" 'Para meter1'" });

            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Para meter1");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Nix)]
        [Category(TestCategory.CompatibleOS.Mac)]
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
        [Category(TestCategory.CompatibleOS.Nix)]
        [Category(TestCategory.CompatibleOS.Mac)]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
                { ["Name"] = "NameToEncrypt" }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

                output.AssertSuccess();
                output.AssertOutput("Hello NameToEncrypt");
        }


        [Test]
        [Category(TestCategory.CompatibleOS.Nix)]
        [Category(TestCategory.CompatibleOS.Mac)]
        public void ShouldCallHelloWithNullVariable()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
                {["Name"] = null});

            output.AssertSuccess();
            output.AssertOutput("Hello");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Nix)]
        [Category(TestCategory.CompatibleOS.Mac)]
        public void ShouldCallHelloWithNullSensitiveVariable()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
                { ["Name"] = null }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            output.AssertSuccess();
            output.AssertOutput("Hello");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Nix)]
        [Category(TestCategory.CompatibleOS.Mac)]
        public void ShouldNotFailOnStdErr()
        {
            var (output, _) = RunScript("stderr.sh");

            output.AssertSuccess();
            output.AssertErrorOutput("hello");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Nix)]
        [Category(TestCategory.CompatibleOS.Mac)]
        public void ShoulFailOnStdErrWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunScript("stderr.sh", new Dictionary<string, string>()
                {[SpecialVariables.Action.FailScriptOnErrorOutput] = "True"});

            output.AssertFailure();
            output.AssertErrorOutput("hello");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ThrowsExceptionOnWindows()
        {
            var (output, _) = RunScript("print-encoded-variable.sh");

            output.AssertErrorOutput("Bash scripts are not supported on this platform");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Nix)]
        [Category(TestCategory.CompatibleOS.Mac)]
        public void ShouldSupportStrictVariableUnset()
        {
            var (output, _) = RunScript("strict-mode.sh");

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==']");
        }
    }
}