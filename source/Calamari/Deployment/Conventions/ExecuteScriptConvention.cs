using System.IO;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.Scripting;

namespace Calamari.Deployment.Conventions
{
    public class ExecuteScriptConvention : Shared.Commands.IConvention
    {
        private readonly IScriptRunner scriptEngine;

        public ExecuteScriptConvention(IScriptRunner scriptEngine)
        {
            this.scriptEngine = scriptEngine;
        }
        
        public void Run(IExecutionContext deployment)
        {
            var variables = deployment.Variables;
            var scriptFile = Path.Combine(deployment.CurrentDirectory, variables.Get(SpecialVariables.Action.Script.ScriptFileName));
            var scriptParameters = variables.Get(SpecialVariables.Action.Script.ScriptParameters);
            Log.VerboseFormat("Executing '{0}'", scriptFile);
            var result = scriptEngine.Execute(new Shared.Scripting.Script(scriptFile, scriptParameters));

            var exitCode = 
                result.ExitCode == 0 && result.HasErrors && variables.GetFlag(SpecialVariables.Action.FailScriptOnErrorOutput)
                    ? -1
                    : result.ExitCode;
            
            Log.SetOutputVariable(SpecialVariables.Action.Script.ExitCode, exitCode.ToString(), variables);
        }
    }
}