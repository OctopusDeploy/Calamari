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
            return cd;
        }

        protected CalamariResult ExecuteScript(IScriptEngine psse, string scriptName, IVariables variables)
        {
            var capture = new CaptureCommandOutput();
            var runner = new CommandLineRunner(capture);
            var result = psse.Execute(new Script(scriptName), variables, runner);
            return new CalamariResult(result.ExitCode, capture);
        }
    }
}
