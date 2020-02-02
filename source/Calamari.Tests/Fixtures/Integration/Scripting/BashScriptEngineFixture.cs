using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Scripting.Bash;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    public class BashScriptEngineFixture : ScriptEngineFixtureBase
    {
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
    }
}
