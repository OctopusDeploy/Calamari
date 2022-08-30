using System;
using System.IO;
using Calamari.Common.Features.Scripting.WindowsPowerShell;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    public class PowerShellScriptEngineFixture : ScriptEngineFixtureBase
    {
        [Test]
        [RequiresNonFreeBSDPlatform]
        public void PowerShellDecryptsVariables()
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath, "Write-Host $mysecrect");
                var result = ExecuteScript(new PowerShellScriptExecutor(), scriptFile.FilePath, GetVariables());
                result.AssertOutput("KingKong");
            }
        }

        [Test]
        [TestCase("true")]
        [TestCase("True")]
        [TestCase("1")]
        [TestCase("2")]
        [RequiresNonFreeBSDPlatform]
        [RequiresPowerShell5OrAbove]
        public void PowerShellCanSetTraceMode(string variableValue)
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath, "Write-Host $mysecrect");
                var calamariVariableDictionary = GetVariables();
                calamariVariableDictionary.Set("Octopus.Action.PowerShell.PSDebug.Trace", variableValue);

                var result = ExecuteScript(new PowerShellScriptExecutor(), scriptFile.FilePath,
                    calamariVariableDictionary);

                result.AssertOutput("KingKong");
                result.AssertOutput("DEBUG:    1+  >>>> Write-Host $mysecrect");
                result.AssertNoOutput("PowerShell tracing is only supported with PowerShell versions 5 and above");

                if (variableValue != "1")
                {
                    //see https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/set-psdebug?view=powershell-6#description
                    //When the Trace parameter has a value of 1, each line of script is traced as it runs.
                    //When the parameter has a value of 2, variable assignments, function calls, and script calls are also traced
                    //we translate "true" to "2"
                    result.AssertOutput("! CALL function 'Import-CalamariModules'");
                }
            }
        }

        [Test]
        [TestCase("true")]
        [TestCase("True")]
        [TestCase("1")]
        [TestCase("2")]
        [RequiresNonFreeBSDPlatform]
        [RequiresPowerShell4]
        public void PowerShell4DoesntSupport(string variableValue)
        {
            //this may cause an `Inconclusive: Outcome value 0 is not understood` error in Rider
            //known bug - https://youtrack.jetbrains.com/issue/RSRP-465549
            if (ScriptingEnvironment.SafelyGetPowerShellVersion().Major != 4)
                Assert.Inconclusive("This test requires PowerShell 4");

            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath, "Write-Host $mysecrect");
                var calamariVariableDictionary = GetVariables();
                calamariVariableDictionary.Set("Octopus.Action.PowerShell.PSDebug.Trace", variableValue);

                var result = ExecuteScript(new PowerShellScriptExecutor(), scriptFile.FilePath, calamariVariableDictionary);

                result.AssertOutput("KingKong");
                result.AssertOutput("Octopus.Action.PowerShell.PSDebug.Trace is enabled, but PowerShell tracing is only supported with PowerShell versions 5 and above. This server is currently running PowerShell version 4.0.");
            }
        }

        [Test]
        [TestCase("0")]
        [TestCase("False")]
        [TestCase("false")]
        [TestCase("")]
        [RequiresNonFreeBSDPlatform]
        public void PowerShellDoesntForceTraceMode(string variableValue)
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath, "Write-Host $mysecrect");
                var calamariVariableDictionary = GetVariables();
                if (!string.IsNullOrEmpty(variableValue))
                    calamariVariableDictionary.Set("Octopus.Action.PowerShell.PSDebug.Trace", variableValue);

                var result = ExecuteScript(new PowerShellScriptExecutor(), scriptFile.FilePath,
                    calamariVariableDictionary);

                result.AssertOutput("KingKong");
                result.AssertNoOutput("DEBUG:    1+  >>>> Write-Host $mysecrect");
            }
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        public void PowerShellWorksWithStrictMode()
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath,
                    "$newVar = $nonExistentVar" + Environment.NewLine + "write-host \"newVar = '$newVar'\"");
                var calamariVariableDictionary = GetVariables();
                calamariVariableDictionary.Set("Octopus.Action.PowerShell.PSDebug.Strict", "true");

                var result = ExecuteScript(new PowerShellScriptExecutor(), scriptFile.FilePath,
                    calamariVariableDictionary);

                result.AssertErrorOutput(" cannot be retrieved because it has not been set.");
                result.AssertFailure();
            }
        }

        [Test]
        [TestCase("false")]
        [TestCase("")]
        [RequiresNonFreeBSDPlatform]
        public void PowerShellDoesntForceStrictMode(string variableValue)
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath,
                    "$newVar = $nonExistentVar" + Environment.NewLine + "write-host \"newVar = '$newVar'\"");
                var calamariVariableDictionary = GetVariables();
                if (!string.IsNullOrEmpty(variableValue))
                    calamariVariableDictionary.Set("Octopus.Action.PowerShell.PSDebug.Strict", variableValue);

                var result = ExecuteScript(new PowerShellScriptExecutor(), scriptFile.FilePath,
                    calamariVariableDictionary);

                result.AssertOutput("newVar = ''");
                result.AssertNoOutput(" cannot be retrieved because it has not been set.");
            }
        }
    }
}
