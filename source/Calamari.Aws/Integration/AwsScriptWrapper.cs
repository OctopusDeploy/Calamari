using System.Collections.Generic;
using Calamari.CloudAccounts;
using Calamari.Common.Features.Scripting;
using Calamari.Extensions;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Aws.Integration
{
    public class AwsScriptWrapper : IScriptWrapper
    {
        readonly ILog log;
        readonly IVariables variables;
        public int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority;
        bool IScriptWrapper.IsEnabled(ScriptSyntax syntax) => true;
        public IScriptWrapper NextWrapper { get; set; }

        public AwsScriptWrapper(ILog log, IVariables variables)
        {
            this.log = log;
            this.variables = variables;
        }
        
        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            var awsEnvironmentVars = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult().EnvironmentVars;
            awsEnvironmentVars.AddRange(environmentVars);

            return NextWrapper.ExecuteScript(
                script, scriptSyntax, 
                commandLineRunner,
                awsEnvironmentVars);
        }
    }
}