using System.ComponentModel;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.Azure;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting.Bash;
using Calamari.Integration.Scripting.ScriptCS;
using Calamari.Integration.Scripting.WindowsPowerShell;
using Octostache;

namespace Calamari.Integration.Scripting
{
    public class CombinedScriptEngine : IScriptEngine
    {
        readonly AzurePowershellContext azurePowershellContext;

        public CombinedScriptEngine()
        {
            this.azurePowershellContext = new AzurePowershellContext();
        }

        public string[] GetSupportedExtensions()
        {
            return CalamariEnvironment.IsRunningOnNix
                ? new[] {ScriptType.ScriptCS.FileExtension(), ScriptType.Bash.FileExtension()}
                : new[] {ScriptType.ScriptCS.FileExtension(), ScriptType.Powershell.FileExtension()};
        }

        public CommandResult Execute(string scriptFile, VariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            var scriptType = Path.GetExtension(scriptFile).TrimStart('.').ToScriptType();
            var engine = GetSpecificScriptEngine(scriptType);

            // When running Powershell against an Azure target, we load the Azure Powershell modules, 
            // and set the Azure subscription
            if (scriptType == ScriptType.Powershell &&
                variables.Get(SpecialVariables.Account.AccountType) == "AzureSubscription")
            {
                var azureContextScriptFile = azurePowershellContext.CreateAzureContextScript(scriptFile, variables);
                return engine.Execute(azureContextScriptFile, variables, commandLineRunner);
            }

            return engine.Execute(scriptFile, variables, commandLineRunner);
        }

        static IScriptEngine GetSpecificScriptEngine(ScriptType scriptType)
        {
            switch (scriptType)
            {
                case ScriptType.Powershell:
                    return new PowerShellScriptEngine();
                case ScriptType.ScriptCS:
                    return new ScriptCSScriptEngine();
                case ScriptType.Bash:
                    return new BashScriptEngine();
                default:
                    throw new InvalidEnumArgumentException("scriptType");
            }
        }
    }
}