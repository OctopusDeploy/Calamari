using System.Collections.Generic;
using System.IO;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Bash
{
    [TestFixture]
    public class BashFixture : CalamariFixture
    {
        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldPrintEncodedVariable()
        {
            var (output, _) = RunScript("print-encoded-variable.sh");

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='U3VwZXI=' value='TWFyaW8gQnJvcw==']");
        }
        
        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldPrintSensitiveVariable()
        {
            var (output, _) = RunScript("print-sensitive-variable.sh");

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='UGFzc3dvcmQ=' value='Y29ycmVjdCBob3JzZSBiYXR0ZXJ5IHN0YXBsZQ==' sensitive='VHJ1ZQ==']");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldCreateArtifact()
        {
            var (output, _) = RunScript("create-artifact.sh");

            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact path='Li9zdWJkaXIvYW5vdGhlcmRpci9teWZpbGU=' name='bXlmaWxl' length='MA==']");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldConsumeParametersWithQuotes()
        {
            var (output, _) = RunScript("parameters.sh", new Dictionary<string, string>()
                { [SpecialVariables.Action.Script.ScriptParameters] = "\"Para meter0\" 'Para meter1'" });

            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Para meter1");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
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
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
                { ["Name"] = "NameToEncrypt" }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

                output.AssertSuccess();
                output.AssertOutput("Hello NameToEncrypt");
        }


        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldCallHelloWithNullVariable()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
                {["Name"] = null});

            output.AssertSuccess();
            output.AssertOutput("Hello");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldCallHelloWithNullSensitiveVariable()
        {
            var (output, _) = RunScript("hello.sh", new Dictionary<string, string>()
                { ["Name"] = null }, sensitiveVariablesPassword: "5XETGOgqYR2bRhlfhDruEg==");

            output.AssertSuccess();
            output.AssertOutput("Hello");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldListVariablenames()
        {
            var (output, _) = RunScript("list-variable-names.sh", new Dictionary<string, string>()
            {
                ["VariableName1"] = "1",
                ["VariableName 2"] = "2",
                ["VariableName3"] = "3",
                ["VariableName '4'"] = "4"
            });

            output.AssertSuccess();
            output.AssertOutput(@"VariableName1");
            output.AssertOutput(@"VariableName 2");
            output.AssertOutput(@"VariableName3");
            output.AssertOutput(@"VariableName  '4'");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldNotFailOnStdErr()
        {
            var (output, _) = RunScript("stderr.sh");

            output.AssertSuccess();
            output.AssertErrorOutput("hello");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShoulFailOnStdErrWithTreatScriptWarningsAsErrors()
        {
            var (output, _) = RunScript("stderr.sh", new Dictionary<string, string>()
                {[SpecialVariables.Action.FailScriptOnErrorOutput] = "True"});

            output.AssertFailure();
            output.AssertErrorOutput("hello");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ThrowsExceptionOnWindows()
        {
            var (output, _) = RunScript("print-encoded-variable.sh");

            output.AssertErrorOutput("Bash scripts are not supported on this platform");
        }
    }
}