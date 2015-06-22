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
        private string sensitiveVariablesPassword;
        private string sensitiveVariablesSalt;

        public RunScriptCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("script=", "Path to the script (PowerShell or ScriptCS) script to execute.", v => scriptFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables (only applicable to offline-drop deployments).", v => sensitiveVariablesPassword = v);
            Options.Add("sensitiveVariablesSalt=", "Base64 encoded initialization-vector used to decrypt sensitive-variables (only applicable to offline-drop deployments).", v => sensitiveVariablesSalt = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);

            var variables = LoadVariables();
            variables.EnrichWithEnvironmentVariables();
            variables.LogVariables();

            return InvokeScript(variables);
        }

        private VariableDictionary LoadVariables()
        {
            if (variablesFile != null && !File.Exists(variablesFile))
                throw new CommandException("Could not find variables file: " + variablesFile);

            if (!string.IsNullOrEmpty(sensitiveVariablesPassword))
            {
               if (string.IsNullOrWhiteSpace(sensitiveVariablesSalt)) 
                throw new CommandException("sensitiveVariablesSalt option must be supplied if sensitiveVariablesPassword option is supplied.");

                return new SensitiveVariables(CalamariPhysicalFileSystem.GetPhysicalFileSystem()).IncludeSensitiveVariables(variablesFile,
                    sensitiveVariablesPassword, sensitiveVariablesSalt);
            }

            return new VariableDictionary(variablesFile);
        }

        private int InvokeScript(VariableDictionary variables)
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
