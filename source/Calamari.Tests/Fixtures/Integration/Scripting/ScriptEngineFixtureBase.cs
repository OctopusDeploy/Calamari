using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    public abstract class ScriptEngineFixtureBase
    {
        protected CalamariVariables GetVariables()
        {
            var cd = new CalamariVariables();
            cd.Set("foo", "bar");
            cd.Set("mysecrect", "KingKong");
            return cd;
        }

        protected CalamariResult ExecuteScript(IScriptExecutor psse, string scriptName, IVariables variables)
        {
            var runner = new TestCommandLineRunner(new InMemoryLog(), variables);
            var result = psse.Execute(new Script(scriptName), variables, runner);
            return new CalamariResult(result.ExitCode, runner.Output);
        }
    }
}
