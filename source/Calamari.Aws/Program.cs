using Calamari.Aws.Integration;
using Calamari.Commands.Support;
using Calamari.Integration.Scripting;
using System.Collections.Generic;
using Autofac;
using Calamari.Aws.Modules;

namespace Calamari.Aws
{
    class Program : Calamari.Program
    {
        public Program(string displayName,
            string informationalVersion,
            string[] environmentInformation,
            ICommand command) : base(displayName, informationalVersion, environmentInformation, command)
        {
            // AwsPowerShellScriptEngine is used to populate the AWS authentication and region environment
            // variables of the process that runs powershell scripts.
            ScriptEngineRegistry.Instance.ScriptEngines[ScriptType.Powershell] = new AwsPowerShellScriptEngine();  
        }

        static int Main(string[] args)
        {
            using (var container = BuildContainer(args))
            {
                using (var scope = container.BeginLifetimeScope(
                    builder =>
                    {
                        builder.RegisterModule(new CalamariProgramModule());
                        builder.RegisterModule(new CalamariCommandsModule());
                    }))
                {
                    return scope.Resolve<Program>().Execute(args);
                }
            }
        }
    }
}