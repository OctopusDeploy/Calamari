using System.Collections.Generic;
using Calamari.CloudAccounts;
using Calamari.Extensions;
using Calamari.Hooks;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Aws.Integration
{
    public class AwsScriptWrapper : ScriptWrapperBase
    {
        readonly ILog log;
        public override int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority;
        public override bool IsEnabled(ScriptSyntax syntax) => true;
        public override IScriptWrapper NextWrapper { get; set; }

        public AwsScriptWrapper(ILog log)
        {
            this.log = log;
        }
        
        protected override CommandResult ExecuteScriptBase (Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            var awsEnvironmentVars = AwsEnvironmentGeneration.Create(log, Variables).GetAwaiter().GetResult().EnvironmentVars;
            awsEnvironmentVars.AddRange(environmentVars);

            return NextWrapper.ExecuteScript(
                script, 
                scriptSyntax, 
                commandLineRunner,
                Variables,
                awsEnvironmentVars);
        }
    }
}