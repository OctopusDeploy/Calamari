using System.Collections.Specialized;
using Calamari.Hooks;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Aws.Integration
{
    public class AwsScriptWrapper : IScriptWrapper
    {
        public int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority;
        public bool Enabled { get; } = true;
        public IScriptWrapper NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars)
        {
            var awsEnvironmentVars = AwsEnvironmentGeneration.Create(variables).GetAwaiter().GetResult().EnvironmentVars;

            return NextWrapper.ExecuteScript(
                script, scriptSyntax, 
                variables, 
                commandLineRunner,
                environmentVars.MergeDictionaries(awsEnvironmentVars));
        }
    }
}