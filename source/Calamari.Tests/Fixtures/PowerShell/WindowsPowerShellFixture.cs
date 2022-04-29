using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.PowerShell
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class WindowsPowerShellFixture : PowerShellFixtureBase
    {
        protected override PowerShellEdition PowerShellEdition => PowerShellEdition.Desktop;

        [Test]
        [Platform]
        // Windows 2016 (has PowerShell 2) will also match Windows 2019 (no PowerShell 2) so have omitted it.
        [TestCase("2", "PSVersion                      2.0", IncludePlatform = "Win2008Server,Win2008ServerR2,Win2012Server,Win2012ServerR2,Windows10")]
        [TestCase("2.0", "PSVersion                      2.0", IncludePlatform = "Win2008Server,Win2008ServerR2,Win2012Server,Win2012ServerR2,Windows10")]
        public void ShouldCustomizePowerShellVersionIfRequested(string customPowerShellVersion, string expectedLogMessage)
        {
            var variables = new CalamariVariables();
            variables.Set(PowerShellVariables.CustomPowerShellVersion, customPowerShellVersion);

            // Let's just use the Hello.ps1 script for something simples
            var output = InvokeCalamariForPowerShell(calamari => calamari
                .Action("run-script")
                .Argument("script", GetFixtureResource("Scripts", "Hello.ps1")), variables);

            if (output.CapturedOutput.AllMessages
                .Select(line => new string(line.ToCharArray().Where(c => c != '\u0000').ToArray()))
                .Any(line => line.Contains(".NET Framework is not installed")))
            {
                Assert.Inconclusive("Version 2.0 of PowerShell is not supported on this machine");
            }

            output.AssertSuccess();
            output.AssertOutput(expectedLogMessage);
            output.AssertOutput("Hello!");
        }

        [Test]
        public void ShouldPrioritizePowerShellScriptsOverOtherSyntaxes()
        {
            var variables = new CalamariVariables();
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.PowerShell), "Write-Host Hello PowerShell");
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.CSharp), "Write-Host Hello CSharp");
            variables.Set(SpecialVariables.Action.Script.ScriptBodyBySyntax(ScriptSyntax.Bash), "echo Hello Bash");

            var output = InvokeCalamariForPowerShell(calamari => calamari
                .Action("run-script"), variables);

            output.AssertSuccess();
            output.AssertOutput("Hello PowerShell");
        }

        [Test]
        public void IncorrectPowerShellEditionShouldThrowException()
        {
            var nonExistentEdition = "WindowsPowerShell";
            var output = RunScript("Hello.ps1",
                new Dictionary<string, string>() {{PowerShellVariables.Edition, nonExistentEdition}});

            output.result.AssertFailure();
            output.result.AssertErrorOutput("Attempted to use 'WindowsPowerShell' edition of PowerShell, but this edition could not be found. Possible editions: Core, Desktop");
        }
    }
}