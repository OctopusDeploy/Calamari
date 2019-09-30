﻿using System.IO;
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
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void PowershellDecryptsSensitiveVariables()
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "ps1")))
            {
                File.WriteAllText(scriptFile.FilePath, "Write-Host $mysecrect");
                var result = ExecuteScript(new PowerShellScriptEngine(), scriptFile.FilePath, GetDictionaryWithSecret());
                result.AssertOutput("KingKong");
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
