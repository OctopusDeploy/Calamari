using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting.Bash;
using Calamari.Integration.ServiceMessages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Bash
{
    [TestFixture]
    [Category(TestEnvironment.CompatableOS.All)]
    public class BashFixture : CalamariFixture
    {
        [Test]
        public void ShouldPrintEncodedVariable()
        {
            var output = Invoke(Calamari()
                .Action("run-script")
                .Argument("script", GetFixtureResouce("Scripts", "PrintEncodedVariable.sh")));

            output.AssertZero();
            output.AssertOutput("##octopus[setVariable name='RG9ua2V5' value='S29uZw==']");
        }

        [Test]
        public void ShouldCallHello()
        {
            var eng = new BashScriptEngine();

            

            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set("Name", "Paul");
            variables.Set("Variable2", "DEF");
            variables.Set("Variable3", "GHI");
            variables.Set("Foo_bar", "Hello");
            variables.Set("Host", "Never");
            variables.Save(variablesFile);

            var runner = new CommandLineRunner(
                new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("script", GetFixtureResouce("Scripts", "Hello.sh"))
                    .Argument("variables", variablesFile));

                output.AssertZero();
                output.AssertOutput("Hello Paul");
            }
        }


        readonly string FixtureDirectory = TestEnvironment.GetTestPath("Fixtures", "Bash");
        private string GetFixtureResouce(params string[] paths)
        {
            return Path.Combine(FixtureDirectory, Path.Combine(paths));
        }
    }
}