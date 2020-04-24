using System.IO;
using Calamari.Common.Variables;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using Calamari.Variables;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    public abstract class ScriptEngineFixtureBase
    {
        protected CalamariVariables GetVariables()
        {
            var cd = new CalamariVariables();
            cd.Set("foo", "bar");
            cd.Set("mysecrect", "KingKong");
            AddScriptRunnerVariables(cd);
            return cd;
        }

        protected CalamariResult ExecuteScript(IScriptExecutor psse, string scriptName, IVariables variables)
        {
            var runner = new TestCommandLineRunner(new InMemoryLog(), variables);
            var result = psse.Execute(new Script(scriptName), variables, runner);
            return new CalamariResult(result.ExitCode, runner.Output);
        }

        public static void AddScriptRunnerVariables(CalamariVariables variables)
        {
            var binDir = Path.GetDirectoryName(typeof(ScriptEngineFixtureBase).Assembly.Location);
            variables.Set(ScriptVariables.ScriptCsPath, Path.Combine(binDir, "ScriptCS"));
            variables.Set(ScriptVariables.FSharpPath, Path.Combine(binDir, "FSharp"));
        }
    }
}
