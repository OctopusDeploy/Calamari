using System.Collections.Specialized;
using Calamari.Deployment;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.Scripting.WindowsPowerShell;

namespace Calamari.Azure.Integration
{
    public class AzurePowerShellScriptEngine : IScriptEngineDecorator
    {
        public IScriptEngine Parent { get; set; }

        public string Name => "Azure";

        public ScriptType[] GetSupportedTypes()
        {
            return new[] { ScriptType.Powershell };
        }

        public CommandResult Execute(
            Script script, 
            CalamariVariableDictionary variables, 
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars)
        {
            var powerShellEngine = new PowerShellScriptEngine();
            if (!string.IsNullOrEmpty(variables.Get(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint)))
            {
                return new AzureServiceFabricPowerShellContext() { Parent = this.Parent }
                    .ExecuteScript(script, variables, commandLineRunner);
            }
            else if (variables.Get(SpecialVariables.Account.AccountType).StartsWith("Azure"))
            {
                return new AzurePowerShellContext() { Parent = this.Parent }
                .ExecuteScript(script, variables, commandLineRunner);
            }

            return powerShellEngine.Execute(script, variables, commandLineRunner, environmentVars);
        }
    }
}