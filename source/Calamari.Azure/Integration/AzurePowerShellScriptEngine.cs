using Calamari.Deployment;
using Calamari.Extensibility;
using Calamari.Features;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Integration.Scripting.WindowsPowerShell;

namespace Calamari.Azure.Integration
{
    public class AzurePowerShellScriptEngine : IScriptEngine
    {
        public string[] GetSupportedExtensions()
        {
            return new[] { ScriptType.Powershell.FileExtension() };
        }

        public CommandResult Execute(Script script, IVariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            var powerShellEngine = new PowerShellScriptEngine();
            if (variables.Get(SpecialVariables.Account.AccountType).StartsWith("Azure"))
            {
                return new AzurePowerShellContext().ExecuteScript(powerShellEngine, script, variables, commandLineRunner);
            }

            return powerShellEngine.Execute(script, variables, commandLineRunner);
        }
    }
}