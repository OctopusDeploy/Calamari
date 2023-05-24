using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Aws.Deployment;
using Calamari.CloudAccounts;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;

namespace Calamari.Aws.Integration
{
    public class AwsScriptWrapper : IScriptWrapper
    {
        readonly ILog log;
        readonly IVariables variables;
        public int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority;

        bool IScriptWrapper.IsEnabled(ScriptSyntax syntax)
        {
            var accountType = variables.Get(SpecialVariables.Account.AccountType);
            var useAwsInstanceRole = variables.Get(AwsSpecialVariables.Authentication.UseInstanceRole);
            return accountType == "AmazonWebServicesAccount" ||
                string.Equals(useAwsInstanceRole, bool.TrueString, StringComparison.InvariantCultureIgnoreCase);
        }

        public IScriptWrapper NextWrapper { get; set; }

        public Func<Task<bool>> VerifyAmazonLogin { get; set; }

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
            var awsEnvironmentVars = AwsEnvironmentGeneration.Create(log, variables, VerifyAmazonLogin).GetAwaiter().GetResult().EnvironmentVars;
            awsEnvironmentVars.AddRange(environmentVars);

            return NextWrapper.ExecuteScript(
                script, scriptSyntax,
                commandLineRunner,
                awsEnvironmentVars);
        }
    }
}