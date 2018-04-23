using Calamari.Aws.Integration;
using Calamari.Commands.Support;
using Calamari.Integration.Scripting;
using System.Collections.Generic;
using Autofac;

namespace Calamari.Aws
{
    class Program : Calamari.Program
    {
        public Program(string displayName,
            string informationalVersion,
            string[] environmentInformation,
            IEnumerable<ICommand> commands) : base(displayName, informationalVersion, environmentInformation, commands)
        {
            // AwsPowerShellScriptEngine is used to populate the AWS authentication and region environment
            // variables of the process that runs powershell scripts.
            ScriptEngineRegistry.Instance.ScriptEngines[ScriptType.Powershell] = new AwsPowerShellScriptEngine();  
        }

        static int Main(string[] args)
        {
            using (var container = BuildContainer())
            {
                return container.Resolve<Program>().Execute(args);
            }
        }
    }
}