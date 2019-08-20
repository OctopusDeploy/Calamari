using System.Collections.Generic;
using Calamari.Extensions;
using Calamari.Hooks;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Aws.Integration
{
    public class AwsScriptWrapper : IScriptWrapper
    {
        public int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority;
        bool IScriptWrapper.IsEnabled(ScriptSyntax syntax) => true;
        public IScriptWrapper NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            var awsEnvironmentVars = AwsEnvironmentGeneration.Create(variables).GetAwaiter().GetResult().EnvironmentVars;
            awsEnvironmentVars.MergeDictionaries(environmentVars);

            Log.Verbose("Executing the AWS script");
            return NextWrapper.ExecuteScript(
                script, scriptSyntax, 
                variables, 
                commandLineRunner,
                awsEnvironmentVars);
        }
    }
}