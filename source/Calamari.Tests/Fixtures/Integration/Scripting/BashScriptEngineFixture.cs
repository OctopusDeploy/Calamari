using System.IO;
using Calamari.Common.Features.Scripting.Bash;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Integration.FileSystem;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    public class BashScriptEngineFixture : ScriptEngineFixtureBase
    {
        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void BashDecryptsVariables()
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "sh")))
            {
                File.WriteAllText(scriptFile.FilePath, "#!/bin/bash\necho $(get_octopusvariable \"mysecrect\")");
                var result = ExecuteScript(new BashScriptExecutor(), scriptFile.FilePath, GetVariables());
                result.AssertOutput("KingKong");
            }
        }
    }
}
