using System.IO;
using Calamari.Common.Features.Scripting.DotnetScript;
using Calamari.Common.Features.Scripting.ScriptCS;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    public class CSharpScriptEngineFixture : ScriptEngineFixtureBase
    {
        [Category(TestCategory.ScriptingSupport.DotnetScript)]
        [Test, RequiresDotNetCore]
        public void DotnetScript_CSharpDecryptsVariables()
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "cs")))
            {
                var variables = GetVariables();
                variables.Add(ScriptVariables.UseDotnetScript, bool.TrueString);
                File.WriteAllText(scriptFile.FilePath, "System.Console.WriteLine(OctopusParameters[\"mysecrect\"]);");
                var result = ExecuteScript(new DotnetScriptExecutor(), scriptFile.FilePath, variables);
                result.AssertOutput("KingKong");
            }
        }

        [Category(TestCategory.ScriptingSupport.ScriptCS)]
        [Test, RequiresMonoVersion400OrAbove, RequiresDotNet45, RequiresMonoVersionBefore(5, 14, 0)]
        public void ScriptCS_CSharpDecryptsVariables()
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "cs")))
            {
                File.WriteAllText(scriptFile.FilePath, "System.Console.WriteLine(Octopus.Parameters[\"mysecrect\"]);");
                var result = ExecuteScript(new ScriptCSScriptExecutor(), scriptFile.FilePath, GetVariables());
                result.AssertOutput("KingKong");
            }
        }
    }
}
