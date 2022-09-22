using System.IO;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PowerShell
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
    public class PowerShellOnLinuxOrMacFixture : PowerShellFixtureBase
    {
        [SetUp]
        public void SetUp()
        {
            CommandLineRunner clr = new CommandLineRunner(ConsoleLog.Instance, new CalamariVariables());
            var result = clr.Execute(new CommandLineInvocation("pwsh", "--version") { OutputToLog = false });
            if (result.HasErrors)
                Assert.Inconclusive("PowerShell Core is not installed on this machine");
        }

        [Test]
        public void ShouldRunBashInsteadOfPowerShell()
        {
            var variablesFile = Path.GetTempFileName();

            var variables = new CalamariVariables();
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