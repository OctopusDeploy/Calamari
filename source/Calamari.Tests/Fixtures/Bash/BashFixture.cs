using System.IO;
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
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "print-encoded-variable.sh")));

            output.AssertSuccess();
            output.AssertOutput("##octopus[setVariable name='U3VwZXI=' value='TWFyaW8gQnJvcw==']");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldCreateArtifact()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "create-artifact.sh")));

            output.AssertSuccess();
            output.AssertOutput("##octopus[createArtifact path='Li9zdWJkaXIvYW5vdGhlcmRpci9teWZpbGU=' name='bXlmaWxl' length='MA==']");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldConsumeParametersWithQuotes()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "parameters.sh"))
                .Argument("scriptParameters", "\"Para meter0\" 'Para meter1'"));

            output.AssertSuccess();
            output.AssertOutput("Parameters Para meter0Para meter1");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldCallHello()
        {
            var variablesFile = Path.GetTempFileName();
            var variables = new VariableDictionary();
            variables.Set("Name", "Paul");
            variables.Set("Variable2", "DEF");
            variables.Set("Variable3", "GHI");
            variables.Set("Foo_bar", "Hello");
            variables.Set("Host", "Never");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", GetFixtureResouce("Scripts", "hello.sh"))
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Hello Paul");
            }
        }


        [Test]
        [Category(TestEnvironment.CompatibleOS.Nix)]
        [Category(TestEnvironment.CompatibleOS.Mac)]
        public void ShouldCallHelloWithSensitiveVariable()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Name", "NameToEncrypt");
            variables.SaveEncrypted("5XETGOgqYR2bRhlfhDruEg==", variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", GetFixtureResouce("Scripts", "hello.sh"))
                    .Argument("sensitiveVariables", variablesFile)
                    .Argument("sensitiveVariablesPassword", "5XETGOgqYR2bRhlfhDruEg=="));

                output.AssertSuccess();
                output.AssertOutput("Hello NameToEncrypt");
            }
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ThrowsExceptionOnWindows()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "print-encoded-variable.sh")));


            output.AssertErrorOutput("Bash scripts are not supported on this platform");
        }
    }
}