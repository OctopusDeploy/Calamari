using Calamari.Azure.Integration;
using Calamari.Commands.Support;
using Calamari.Integration.Scripting;

namespace Calamari.Azure
{
    class Program : Calamari.Program
    {
        public Program()
        {
            ScriptEngineRegistry.Instance.ScriptEngines[ScriptType.Powershell] = new AzurePowerShellScriptEngine();            
        }

        public new static int Main(string[] args)
        {
            return Calamari.Program.Main(args);
        }

        protected override void RegisterCommandAssemblies()
        {
            CommandLocator.Instance.RegisterAssemblies(typeof(Calamari.Program).Assembly, typeof(Program).Assembly);
        }
    }
}
