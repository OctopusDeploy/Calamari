using Calamari.Deployment;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.Scripting.WindowsPowerShell;

namespace Calamari.Azure.Integration
{
    public class AzurePowerShellScriptEngine : IScriptEngine
    {
        public ScriptType[] GetSupportedTypes()
        {
            return new[] { ScriptType.Powershell };
        }

        public CommandResult Execute(Script script, CalamariVariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            var powerShellEngine = new PowerShellScriptEngine();
            if (!string.IsNullOrEmpty(variables.Get(SpecialVariables.Action.Azure.FabricConnectionEndpoint)))
            {
                return new AzureServiceFabricPowerShellContext().ExecuteScript(powerShellEngine, script, variables, commandLineRunner);
            }
            else if (variables.Get(SpecialVariables.Account.AccountType).StartsWith("Azure"))
            {
                return new AzurePowerShellContext().ExecuteScript(powerShellEngine, script, variables, commandLineRunner);
            }

            return powerShellEngine.Execute(script, variables, commandLineRunner);
        }
    }
}