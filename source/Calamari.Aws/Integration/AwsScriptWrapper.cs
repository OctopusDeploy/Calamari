using System.Collections.Generic;
using Calamari.Extensions;
using Calamari.Hooks;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Aws.Integration
{
    public class AwsScriptWrapper : IScriptWrapper
    {
        readonly IVariables variables;
        readonly ILog log;
        public int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority;
        bool IScriptWrapper.IsEnabled(ScriptSyntax syntax) => true;
        public IScriptWrapper NextWrapper { get; set; }

        public AwsScriptWrapper(IVariables variables, ILog log)
        {
            this.variables = variables;
            this.log = log;
        }
        
        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            var awsEnvironmentVars = AwsEnvironmentGeneration.Create(variables, log).GetAwaiter().GetResult().EnvironmentVars;
            awsEnvironmentVars.MergeDictionaries(environmentVars);

            return NextWrapper.ExecuteScript(
                script, scriptSyntax, 
                commandLineRunner,
                awsEnvironmentVars);
        }
    }
}