using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.ScriptCS
{
    [TestFixture]
    [Category(TestEnvironment.CompatableOS.Windows)]
    public class ScriptCSFixture : CalamariFixture
    {
        [Test, RequiresDotNet45]
        public void ShouldCreateArtifacts()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", MapSamplePath("Scripts\\CanCreateArtifact.csx")));

            output.AssertZero();
            output.AssertOutput("##octopus[createArtifact path='QzpcUGF0aFxGaWxlLnR4dA==' name='RmlsZS50eHQ=']");
        }

        [Test, RequiresDotNet45]
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
                    .Argument("script", MapSamplePath("Scripts\\Hello.csx"))
                    .Argument("variables", variablesFile));

                output.AssertZero();
                output.AssertOutput("Hello Paul");
            }
        }
    }
}