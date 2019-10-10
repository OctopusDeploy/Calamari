using System.IO;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.PowerShell
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
    public class PowerShellOnLinuxOrMacFixtureBase : PowerShellFixtureBase
    {
        [SetUp]
        public void SetUp()
        {
            CommandLineRunner clr = new CommandLineRunner(new IgnoreCommandOutput());
            var result = clr.Execute(new CommandLineInvocation("pwsh", "--version")); 
            if (result.HasErrors)
                Assert.Inconclusive("PowerShell Core is not installed on this machine");
        }
        
        [Test]
        public void ShouldRunBashInsteadOfPowerShell()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new VariableDictionary();
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.PowerShell), "Write-Host Hello PowerShell");
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.CSharp), "Write-Host Hello CSharp");
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.Bash), "echo Hello Bash");
            variables.Save(variablesFile);

            using (new TemporaryFile(variablesFile))
            {
                var output = Invoke(Calamari()
                    .Action("run-script")
                    .Argument("variables", variablesFile));

                output.AssertSuccess();
                output.AssertOutput("Hello Bash");
            }
        }

        protected override PowerShellEdition PowerShellEdition => PowerShellEdition.Core;
    }
}