using Calamari.Azure.Integration;
using Calamari.Commands.Support;
using Calamari.Integration.Scripting;
using Calamari.Util.Environments;
using System.Reflection;

namespace Calamari.Azure
{
    class Program : Calamari.Program
    {
        public Program() : base("Calamari.Azure", typeof(Azure.Program).Assembly.GetInformationalVersion(), EnvironmentHelper.SafelyGetEnvironmentInformation())
        {
            ScriptEngineRegistry.Instance.ScriptEngines[ScriptType.Powershell] = new AzurePowerShellScriptEngine();            
        }

        static int Main(string[] args)
        {
            var program = new Azure.Program();
            return program.Execute(args);
        }

        protected override void RegisterCommandAssemblies()
        {
            CommandLocator.Instance.RegisterAssemblies(typeof(Calamari.Program).Assembly, typeof(Program).Assembly);
        }
    }
}
