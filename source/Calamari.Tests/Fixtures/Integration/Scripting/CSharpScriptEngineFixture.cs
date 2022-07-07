using System.IO;
using Calamari.Common.Features.Scripting.DotnetScript;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    public class CSharpScriptEngineFixture : ScriptEngineFixtureBase
    {
        [Category(TestCategory.ScriptingSupport.DotnetScript)]
        [Test, RequiresDotNetCore]
        public void CSharpDecryptsVariables()
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "cs")))
            {
                File.WriteAllText(scriptFile.FilePath, "System.Console.WriteLine(Octopus.Parameters[\"mysecrect\"]);");
                var result = ExecuteScript(new DotnetScriptExecutor(), scriptFile.FilePath, GetVariables());
                result.AssertOutput("KingKong");
            }
        }
    }
}
