using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Aws.Deployment;
using Calamari.CloudAccounts;
using Calamari.CloudAccounts.Aws;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Integration;

public class AwsScriptWrapper(ILog log, IVariables variables) : IScriptWrapper
{
    public int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority;

    bool IScriptWrapper.IsEnabled(ScriptSyntax syntax)
    {
        var accountType = variables.Get(SpecialVariables.Account.AccountType);
        var awsAccountVariable = variables.Get(AwsSpecialVariables.Authentication.AwsAccountVariable);
        var useAwsInstanceRole = variables.Get(AwsSpecialVariables.Authentication.UseInstanceRole);
        return accountType == "AmazonWebServicesAccount" ||
               accountType == "AmazonWebServicesOidcAccount" ||
               !awsAccountVariable.IsNullOrEmpty() ||
               string.Equals(useAwsInstanceRole, bool.TrueString, StringComparison.InvariantCultureIgnoreCase);
    }

    public IScriptWrapper NextWrapper { get; set; }

    public Func<Task<bool>> VerifyAmazonLogin { get; init; }

    public CommandResult ExecuteScript(Script script,
                                       ScriptSyntax scriptSyntax,
                                       ICommandLineRunner commandLineRunner,
                                       Dictionary<string, string> environmentVars)
    {
        Guard.NotNull(environmentVars, "Environment variables cannot be null");

        
        var awsEnvironmentVars = AwsEnvironmentGeneration.Create(log, variables, VerifyAmazonLogin).GetAwaiter().GetResult().EnvironmentVars;
        awsEnvironmentVars.AddRange(environmentVars);

        // We force null-suppression here, but IScriptRunner needs an overhaul on how this is setup
        return NextWrapper!.ExecuteScript(
                                         script,
                                         scriptSyntax,
                                         commandLineRunner,
                                         awsEnvironmentVars);
    }
}