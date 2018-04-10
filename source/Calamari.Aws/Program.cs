using System;
using System.Diagnostics;
using Calamari.Aws.Integration;
using Calamari.Commands.Support;
using Calamari.Integration.Scripting;
using Calamari.Util.Environments;

namespace Calamari.Aws
{
    class Program : Calamari.Program
    {
        public Program() : base("Calamari.Aws", typeof(Program).Assembly.GetInformationalVersion(), EnvironmentHelper.SafelyGetEnvironmentInformation())
        {
            // AwsPowerShellScriptEngine is used to populate the AWS authentication and region environment
            // variables of the process that runs powershell scripts.
            ScriptEngineRegistry.Instance.ScriptEngines[ScriptType.Powershell] = new AwsPowerShellScriptEngine();  
        }

        static int Main(string[] args)
        {
            var program = new Program();
            return program.Execute(args);
        }

        protected override void RegisterCommandAssemblies()
        {
            CommandLocator.Instance.RegisterAssemblies(typeof(Calamari.Program).Assembly, typeof(Program).Assembly);
        }
    }
}