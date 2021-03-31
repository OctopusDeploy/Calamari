using System.IO;
using Calamari.Common.Features.Scripting.ScriptCS;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    public class CSharpScriptEngineFixture : ScriptEngineFixtureBase
    {
        [Category(TestCategory.ScriptingSupport.ScriptCS)]
        [Test, RequiresMonoVersion400OrAbove, RequiresDotNet45, RequiresMonoVersionBefore(5, 14, 0)]
        public void CSharpDecryptsVariables()
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
