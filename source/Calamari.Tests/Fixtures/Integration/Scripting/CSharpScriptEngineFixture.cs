using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Scripting.ScriptCS;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    public class CSharpScriptEngineFixture : ScriptEngineFixtureBase
    {
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
    }
}
