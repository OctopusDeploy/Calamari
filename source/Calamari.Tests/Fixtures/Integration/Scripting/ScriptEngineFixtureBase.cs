using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;

namespace Calamari.Tests.Fixtures.Integration.Scripting
{
    public abstract class ScriptEngineFixtureBase
    {
        protected CalamariVariableDictionary GetVariables()
        {
            var cd = new CalamariVariableDictionary();
            cd.Set("foo", "bar");
            cd.Set("mysecrect", "KingKong");
            return cd;
        }

        protected CalamariResult ExecuteScript(IScriptEngine psse, string scriptName, CalamariVariableDictionary variables)
        {
            var capture = new CaptureCommandOutput();
            var runner = new CommandLineRunner(capture);
            var result = psse.Execute(new Script(scriptName), variables, runner);
            return new CalamariResult(result.ExitCode, capture);
        }
    }
}
