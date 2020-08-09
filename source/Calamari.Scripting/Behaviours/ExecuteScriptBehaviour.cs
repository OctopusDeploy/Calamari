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
        private readonly IScriptEngine scriptEngine;
        private readonly ICommandLineRunner commandLineRunner;

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
            var scriptFile = Path.Combine(context.CurrentDirectory, variables.Get(ScriptVariables.ScriptFileName));
            var scriptParameters = variables.Get(SpecialVariables.Action.Script.ScriptParameters);
            Log.VerboseFormat("Executing '{0}'", scriptFile);
            var result = scriptEngine.Execute(new Script(scriptFile, scriptParameters), variables, commandLineRunner);

            var exitCode =
                result.ExitCode == 0 && result.HasErrors && variables.GetFlag(SpecialVariables.Action.FailScriptOnErrorOutput)
                    ? -1
                    : result.ExitCode;

            Log.SetOutputVariable(SpecialVariables.Action.Script.ExitCode, exitCode.ToString(), variables);

            return this.CompletedTask();
        }
    }
}