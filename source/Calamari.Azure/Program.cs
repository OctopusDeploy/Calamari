using Autofac;
using Calamari.Azure.Integration;
using Calamari.Commands.Support;
using Calamari.Integration.Scripting;
using System.Collections.Generic;
using Calamari.Azure.Modules;

namespace Calamari.Azure
{
    class Program : Calamari.Program
    {
        public Program(string displayName,
            string informationalVersion,
            string[] environmentInformation,
            IEnumerable<ICommand> commands) : base(displayName, informationalVersion, environmentInformation, commands)
        {
            ScriptEngineRegistry.Instance.ScriptEngines[ScriptType.Powershell] = new AzurePowerShellScriptEngine();            
        }

        static int Main(string[] args)
        {
            using (var container = BuildContainer())
            {
                using (var scope = container.BeginLifetimeScope(
                    builder =>
                    {
                        builder.RegisterModule(new CalamariProgramModule());
                        builder.RegisterModule(new CalamariCommandsModule());
                    }))
                {
                    return container.Resolve<Program>().Execute(args);
                }
            }
        }
    }
}
