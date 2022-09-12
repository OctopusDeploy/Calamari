using System;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Scripting;
using NUnit.Framework;
using Calamari.Testing;

namespace Calamari.AzureScripting.Tests
{
    [TestFixture]
    class AzurePowerShellCommandFixture
    {
        string? clientId;
        string? clientSecret;
        string? tenantId;
        string? subscriptionId;

        [OneTimeSetUp]
        public void Setup()
        {
            clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
        }

        [Test]
        [WindowsTest]
        [RequiresPowerShell5OrAbove]
        public void ExecuteAnInlineWindowsPowerShellScript()
        {
            var psScript = @"
$ErrorActionPreference = 'Continue'
az --version
Get-AzureEnvironment
az group list";

            CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     // TODO: When migrating to Calamari repo switch back to variables.
                                                     context.Variables.Add("Octopus.Action.Script.ScriptSource", "Inline");
                                                     context.Variables.Add(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
                                                     context.Variables.Add(ScriptVariables.ScriptBody, psScript);
                                                 })
                                    .Execute();
        }

        [Test]
        [RequiresPowerShell5OrAbove]
        public void ExecuteAnInlinePowerShellCoreScript()
        {
            var psScript = @"
$ErrorActionPreference = 'Continue'
az --version
Get-AzureEnvironment
az group list";

            CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(Calamari.Common.Plumbing.Variables.PowerShellVariables.Edition, "Core");
                                                     context.Variables.Add("Octopus.Action.Script.ScriptSource", "Inline");
                                                     context.Variables.Add(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
                                                     context.Variables.Add(ScriptVariables.ScriptBody, psScript);
                                                 })
                                    .Execute();
        }

        [Test]
        [RequiresPowerShell5OrAbove]
        public void ExecuteAnInlinePowerShellCoreScriptAgainstAnInvalidAzureEnvironment()
        {
            var psScript = @"
$ErrorActionPreference = 'Continue'
az --version
Get-AzureEnvironment
az group list";

            CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                              .WithArrange(context =>
                                           {
                                               AddDefaults(context);
                                               context.Variables.Add("Octopus.Action.Azure.Environment", "NotARealAzureEnvironment");
                                               context.Variables.Add(PowerShellVariables.Edition, "Core");
                                               context.Variables.Add("Octopus.Action.Script.ScriptSource", "Inline");
                                               context.Variables.Add(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
                                               context.Variables.Add(ScriptVariables.ScriptBody, psScript);
                                           })
                              .Execute(false); // Should fail due to invalid azure environment
        }

        void AddDefaults(CommandTestBuilderContext context)
        {
            context.Variables.Add("Octopus.Account.AccountType", "AzureServicePrincipal");
            context.Variables.Add("Octopus.Action.Azure.SubscriptionId", subscriptionId);
            context.Variables.Add("Octopus.Action.Azure.TenantId", tenantId);
            context.Variables.Add("Octopus.Action.Azure.ClientId", clientId);
            context.Variables.Add("Octopus.Action.Azure.Password", clientSecret);
        }
    }
}