using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.ScriptCS
{
    [TestFixture]
    public class ScriptCSFixture : CalamariFixture
    {
        [Test]
        public void ShouldPrintEncodedVariable()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "PrintEncodedVariable.csx")));

            output.AssertZero();
            output.AssertOutput("##octopus[setVariable name='RG9ua2V5' value='S29uZw==']");
        }

        [Test]
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
                    .Argument("script", GetFixtureResouce("Scripts", "Hello.csx"))
                    .Argument("variables", variablesFile));

                output.AssertZero();
                output.AssertOutput("Hello Paul");
            }
        }
    }
}