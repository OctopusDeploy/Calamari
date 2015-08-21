using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.ServiceMessages;
using Octostache;

namespace Calamari.Commands
{
    [Command("run-script", Description = "Invokes a PowerShell or ScriptCS script")]
    public class RunScriptCommand : Command
    {
        private string variablesFile;
        private string scriptFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;
        private string sensitiveVariablesSalt;

        public RunScriptCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("script=", "Path to the script (PowerShell or ScriptCS) script to execute.", v => scriptFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables (only applicable to offline-drop deployments).", v => sensitiveVariablesPassword = v);
            Options.Add("sensitiveVariablesSalt=", "Base64 encoded initialization-vector used to decrypt sensitive-variables (only applicable to offline-drop deployments).", v => sensitiveVariablesSalt = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword, sensitiveVariablesSalt);
            variables.EnrichWithEnvironmentVariables();
            variables.LogVariables();

            return InvokeScript(variables);
        }
        
        private int InvokeScript(CalamariVariableDictionary variables)
        {
            if (!File.Exists(scriptFile))
                throw new CommandException("Could not find script file: " + scriptFile);

            var scriptEngine = new CombinedScriptEngine();
            var runner = new CommandLineRunner(
                new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var result = scriptEngine.Execute(scriptFile, variables, runner);
            return result.ExitCode;
        }
    }
}
