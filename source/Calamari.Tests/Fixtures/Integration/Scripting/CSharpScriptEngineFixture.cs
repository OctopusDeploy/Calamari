using System;
using System.IO;
using Calamari.Common.Features.Scripting.DotnetScript;
using Calamari.Common.Features.Scripting.ScriptCS;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    [TestFixture]
    public class CSharpScriptEngineFixture : ScriptEngineFixtureBase
    {
        [Category(TestCategory.ScriptingSupport.DotnetScript)]
        [Test]
        public void DotnetScript_CSharpDecryptsVariables()
        {
            using (var scriptFile = new TemporaryFile(Path.ChangeExtension(Path.GetTempFileName(), "cs")))
            {
                var variables = GetVariables();
                variables.Add(ScriptVariables.UseDotnetScript, bool.TrueString);
                File.WriteAllText(scriptFile.FilePath, "System.Console.WriteLine(OctopusParameters[\"mysecrect\"]);");
                var commandLineRunner = new TestCommandLineRunner(new InMemoryLog(), new CalamariVariables());
                var result = ExecuteScript(new DotnetScriptExecutor(commandLineRunner, Substitute.For<ILog>()), scriptFile.FilePath, variables);
                result.AssertOutput("KingKong");
            }
        }
    }
}
