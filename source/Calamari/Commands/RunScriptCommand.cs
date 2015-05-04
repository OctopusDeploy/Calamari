using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
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

        public RunScriptCommand()
        {
            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("script=", "Path to the script (PowerShell or ScriptCS) script to execute.", v => scriptFile = Path.GetFullPath(v));
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

            return new VariableDictionary(string.IsNullOrWhiteSpace(variablesFile) ? null : variablesFile);
        }

        private int InvokeScript(VariableDictionary variables)
        {
            if (!File.Exists(scriptFile))
                throw new CommandException("Could not find script file: " + scriptFile);

            var engine = ScriptEngineSelector.GetScriptEngineSelector().SelectEngine(scriptFile);
            var runner = new CommandLineRunner(
                new SplitCommandOutput(new ConsoleCommandOutput(), new ServiceMessageCommandOutput(variables)));
            var result = engine.Execute(scriptFile, variables, runner);
            return result.ExitCode;
        }
    }
}
