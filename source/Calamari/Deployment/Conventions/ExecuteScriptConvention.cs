using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Deployment.Conventions
{
    public class ExecuteScriptConvention : IInstallConvention
    {
        private readonly string scriptFile;
        private readonly string scriptParameters;
        private readonly IScriptEngine scriptEngine;
        private readonly ICommandLineRunner commandLineRunner;

        public ExecuteScriptConvention(string scriptFile, string scriptParameters, IScriptEngine scriptEngine, ICommandLineRunner commandLineRunner)
        {
            this.scriptFile = scriptFile;
            this.scriptParameters = scriptParameters;
            this.scriptEngine = scriptEngine;
            this.commandLineRunner = commandLineRunner;
        }
        
        public void Install(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            
            Log.VerboseFormat("Executing '{0}'", scriptFile);
            var result = scriptEngine.Execute(new Script(scriptFile, scriptParameters), variables, commandLineRunner);

            var exitCode = 
                result.ExitCode == 0 && result.HasErrors && variables.GetFlag(SpecialVariables.Action.FailScriptOnErrorOutput)
                    ? -1
                    : result.ExitCode;
            
            Log.SetOutputVariable(SpecialVariables.Action.Script.ExitCode, exitCode.ToString(), variables);
        }
    }
}