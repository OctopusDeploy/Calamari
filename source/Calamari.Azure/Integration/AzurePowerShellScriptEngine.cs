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
            Guard.NotNull(script, "script can not be null");
            Guard.NotNull(variables, "variables can not be null");
            Guard.NotNull(commandLineRunner, "commandLineRunner can not be null");
            Guard.NotNull(environmentVars, "environmentVars can not be null");

            if (!string.IsNullOrEmpty(variables.Get(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint)))
            {
                return new AzureServiceFabricPowerShellContext() { Parent = this.Parent }
                    .ExecuteScript(script, variables, commandLineRunner);
            }
            // TODO: The Azzure account needs to contribute these variables.
            else if (true || variables.Get(SpecialVariables.Account.AccountType).StartsWith("Azure"))
            {
                return new AzurePowerShellContext() { Parent = this.Parent }
                    .ExecuteScript(script, variables, commandLineRunner);
            }

            return Parent.Execute(script, variables, commandLineRunner, environmentVars);
        }
    }
}