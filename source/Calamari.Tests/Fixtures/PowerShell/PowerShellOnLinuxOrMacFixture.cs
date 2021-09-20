using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Tests.Helpers;
using FluentAssertions;
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
        
        [Test]
        [TestCase("true", 1)]
        [TestCase("false", 0)]
        [TestCase("1", 1)]
        [TestCase("0", 0)]
        [TestCase(null, 0)]
        public void ScriptWithPowerShellProgressAndOverrideOutputVariableDefinedCorrectlyWritesExpectedWarningAfter(string progressVariable, int expectedOutputCount)
        {
            var additionalVariables = new Dictionary<string, string>
            {
                [PowerShellVariables.OutputPowerShellProgress] = progressVariable
            };
            var (output, _) = RunPowerShellScript("PowerShellProgress.ps1", additionalVariables);

            output.AssertSuccess();
            output.CapturedOutput.AllMessages.Where(x => x.EndsWith(" ##octopus[stdout-warning]")).Should().HaveCount(expectedOutputCount);
        }

        protected override PowerShellEdition PowerShellEdition => PowerShellEdition.Core;
    }
}