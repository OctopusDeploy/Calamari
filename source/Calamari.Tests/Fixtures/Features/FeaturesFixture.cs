using System;
using System.IO;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Features
{
    [TestFixture]
    public class FeaturesFixture : CalamariFixture
    {

        [Test]
        public void DitIr()
        {
            var variablesFile = Path.GetTempFileName();
            
            var variables = new VariableDictionary();
            variables.Set(SpecialVariables.Action.Script.Path, GetFixtureResouce("Scripts", "Hello.ps1"));
            variables.Set(SpecialVariables.Action.Script.Parameters, "Cake");
            variables.Save(variablesFile);
            
            using (new TemporaryFile(variablesFile))
            {
                var output = InProcessInvoke(InProcessCalamari()
                    .Action("run-feature")
                    .Argument("feature", "RunScript")
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Hello!");
            }
        }


        [Test]
        [Description("Proves packaged scripts can have variables substituted into them before running")]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldSubstituteVariablesInPackagedScriptsIfRequested()
        {
            var variablesFile = Path.GetTempFileName();
            var variables = new VariableDictionary();
            variables.Set("Octopus.Environment.Name", "Production");
            variables.Set(SpecialVariables.Action.Script.Path, "Deploy.ps1");
            variables.Set(SpecialVariables.Action.Script.PackagePath, GetFixtureResouce("Packages", "PackagedScript.1.0.0.zip"));
            variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, "True");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = InProcessInvoke(InProcessCalamari()
                     .Action("run-feature")
                    .Argument("feature", "RunScript")
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Extracting package");
                output.AssertOutput("Substituting variables");
                output.AssertOutput("OctopusParameter: Production");
                output.AssertOutput("InlineVariable: Production");
                output.AssertOutput("VariableSubstitution: Production");
            }
        }
    }
}