using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Scripting
{
    public class ExecuteScriptBehaviour : IDeployBehaviour
    {
        readonly IScriptEngine scriptEngine;
        readonly ICommandLineRunner commandLineRunner;

        public ExecuteScriptBehaviour(IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner)
        {
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            var variables = context.Variables;
            var scriptFileName = variables.Get(ScriptVariables.ScriptFileName);
            if (scriptFileName == null)
                throw new InvalidOperationException($"{ScriptVariables.ScriptFileName} variable value could not be found.");
            var scriptFile = Path.Combine(context.CurrentDirectory, scriptFileName);
            var scriptParameters = variables.Get(SpecialVariables.Action.Script.ScriptParameters);
            Log.VerboseFormat("Executing '{0}'", scriptFile);
            var result = scriptEngine.Execute(new Script(scriptFile, scriptParameters), variables, commandLineRunner);

            var exitCode =
                result.ExitCode == 0 && result.HasErrors && variables.GetFlag(SpecialVariables.Action.FailScriptOnErrorOutput)
                    ? -1
                    : result.ExitCode;

            Log.SetOutputVariable(SpecialVariables.Action.Script.ExitCode, exitCode.ToString(), variables);

            return Task.CompletedTask;
        }
    }
}