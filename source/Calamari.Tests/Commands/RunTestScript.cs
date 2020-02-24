using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using System.IO;

namespace Calamari.Tests.Commands
{
    /// <summary>
    /// A cut down command that runs a script without any journaling, variable substitution or
    /// other optional steps.
    /// </summary>
    [Command("run-test-script", Description = "Invokes a PowerShell or ScriptCS script")]
    public class RunTestScript : Command
    {
        private string scriptFile;
        private readonly IVariables variables;
        private readonly CombinedScriptEngine scriptEngine;

        public RunTestScript(
            IVariables variables,
            CombinedScriptEngine scriptEngine)
        {
            Options.Add("script=", "Path to the script to execute.", v => scriptFile = Path.GetFullPath(v));

            this.variables = variables;
            this.scriptEngine = scriptEngine;
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            
            var runner = new CommandLineRunner(new ConsoleCommandOutput());
            Log.VerboseFormat("Executing '{0}'", scriptFile);
            var result = scriptEngine.Execute(new Script(scriptFile, ""), variables, runner);

            if (result.ExitCode == 0 && result.HasErrors && variables.GetFlag(SpecialVariables.Action.FailScriptOnErrorOutput, false))
            {
                return -1;
            }

            return result.ExitCode;
        }       
    }
}
