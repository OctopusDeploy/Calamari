using System;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.Scripting.Bash;
using Calamari.Integration.Scripting.ScriptCS;
using Calamari.Integration.Scripting.WindowsPowerShell;
using Calamari.Tests.Fixtures.ScriptCS;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    public class ScriptEngineFixture
    {
        [Test]
        public void PowerShellDecryptsSensitiveVariables()
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath, "Write-Host $mysecrect");
                var result = ExecuteScript(new PowerShellScriptEngine(), scriptFile.FilePath, GetDictionaryWithSecret());
                result.AssertOutput("KingKong");
            }
        }
        
        [Test]
        [TestCase("true")]
        [TestCase("True")]
        [TestCase("1")]
        [TestCase("2")]
        public void PowerShellCanSetTraceMode(string variableValue)
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath, "Write-Host $mysecrect");
                var calamariVariableDictionary = GetDictionaryWithSecret();
                calamariVariableDictionary.Set("Octopus.Action.PowerShell.PSDebug.Trace", variableValue);

                var result = ExecuteScript(new PowerShellScriptEngine(), scriptFile.FilePath, calamariVariableDictionary);
                
                result.AssertOutput("KingKong");
                result.AssertOutput("DEBUG:    1+  >>>> Write-Host $mysecrect");

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
        [TestCase("0")]
        [TestCase("False")]
        [TestCase("false")]
        [TestCase("")]
        public void PowerShellDoesntForceTraceMode(string variableValue)
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath, "Write-Host $mysecrect");
                var calamariVariableDictionary = GetDictionaryWithSecret();
                if (!string.IsNullOrEmpty(variableValue))
                    calamariVariableDictionary.Set("Octopus.Action.PowerShell.PSDebug.Trace", variableValue);

                var result = ExecuteScript(new PowerShellScriptEngine(), scriptFile.FilePath, calamariVariableDictionary);
                
                result.AssertOutput("KingKong");
                result.AssertNoOutput("DEBUG:    1+  >>>> Write-Host $mysecrect");
            }
        }
        
        [Test]
        public void PowerShellWorksWithStrictMode()
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath, "$newVar = $nonExistentVar" + Environment.NewLine + "write-host \"newVar = '$newVar'\"");
                var calamariVariableDictionary = GetDictionaryWithSecret();
                calamariVariableDictionary.Set("Octopus.Action.PowerShell.PSDebug.Strict", "true");

                var result = ExecuteScript(new PowerShellScriptEngine(), scriptFile.FilePath, calamariVariableDictionary);
                
                result.AssertErrorOutput(" cannot be retrieved because it has not been set.");
                result.AssertFailure();
            }
        }
        
        [Test]
        [TestCase("false")]
        [TestCase("")]
        public void PowerShellDoesntForceStrictMode(string variableValue)
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath, "$newVar = $nonExistentVar" + Environment.NewLine + "write-host \"newVar = '$newVar'\"");
                var calamariVariableDictionary = GetDictionaryWithSecret();
                if (!string.IsNullOrEmpty(variableValue))
                    calamariVariableDictionary.Set("Octopus.Action.PowerShell.PSDebug.Strict", variableValue);

                var result = ExecuteScript(new PowerShellScriptEngine(), scriptFile.FilePath, calamariVariableDictionary);
                
                result.AssertOutput("newVar = ''");
                result.AssertNoOutput(" cannot be retrieved because it has not been set.");
            }
        }

        [Category(TestCategory.ScriptingSupport.ScriptCS)]
        [Test, RequiresMonoVersion400OrAbove, RequiresDotNet45]
        public void CSharpDecryptsSensitiveVariables()
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "cs")))
            {
                File.WriteAllText(scriptFile.FilePath, "System.Console.WriteLine(Octopus.Parameters[\"mysecrect\"]);");
                var result = ExecuteScript(new ScriptCSScriptEngine(), scriptFile.FilePath, GetDictionaryWithSecret());
                result.AssertOutput("KingKong");
            }
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void BashDecryptsSensitiveVariables()
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "sh")))
            {
                File.WriteAllText(scriptFile.FilePath, "#!/bin/bash\necho $(get_octopusvariable \"mysecrect\")");
                var result = ExecuteScript(new BashScriptEngine(), scriptFile.FilePath, GetDictionaryWithSecret());
                result.AssertOutput("KingKong");
            }
        }

        private CalamariVariableDictionary GetDictionaryWithSecret()
        {
            var cd = new CalamariVariableDictionary();
            cd.Set("foo", "bar");
            cd.SetSensitive("mysecrect", "KingKong");
            return cd;
        }

        private CalamariResult ExecuteScript(IScriptEngine psse, string scriptName, CalamariVariableDictionary variables)
        {
            var capture = new CaptureCommandOutput();
            var runner = new CommandLineRunner(capture);
            var result = psse.Execute(new Script(scriptName), variables, runner);
            return new CalamariResult(result.ExitCode, capture);
        }
    }
}
