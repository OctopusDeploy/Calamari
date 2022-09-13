using System;
using Calamari.AzureScripting;
using Calamari.Tests.Shared;
using NUnit.Framework;
using Sashimi.Server.Contracts;
using Sashimi.Tests.Shared;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.AzureScripting.Tests
{
    [TestFixture]
    class AzurePowerShellActionHandlerFixture
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

            ActionHandlerTestBuilder.CreateAsync<AzurePowerShellActionHandler, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Inline);
                                                     context.Variables.Add(KnownVariables.Action.Script.Syntax, ScriptSyntax.PowerShell.ToString());
                                                     context.Variables.Add(KnownVariables.Action.Script.ScriptBody, psScript);
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

            ActionHandlerTestBuilder.CreateAsync<AzurePowerShellActionHandler, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(Calamari.Common.Plumbing.Variables.PowerShellVariables.Edition, "Core");
                                                     context.Variables.Add(KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Inline);
                                                     context.Variables.Add(KnownVariables.Action.Script.Syntax, ScriptSyntax.PowerShell.ToString());
                                                     context.Variables.Add(KnownVariables.Action.Script.ScriptBody, psScript);
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

            ActionHandlerTestBuilder.CreateAsync<AzurePowerShellActionHandler, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add("Octopus.Action.Azure.Environment", "NotARealAzureEnvironment");
                                                     context.Variables.Add(Calamari.Common.Plumbing.Variables.PowerShellVariables.Edition, "Core");
                                                     context.Variables.Add(KnownVariables.Action.Script.ScriptSource, KnownVariableValues.Action.Script.ScriptSource.Inline);
                                                     context.Variables.Add(KnownVariables.Action.Script.Syntax, ScriptSyntax.PowerShell.ToString());
                                                     context.Variables.Add(KnownVariables.Action.Script.ScriptBody, psScript);
                                                 })
                                    .Execute(false); // Should fail due to invalid azure environment
        }

        void AddDefaults(TestActionHandlerContext<Program> context)
        {
            context.Variables.Add(KnownVariables.Account.AccountType, "AzureServicePrincipal");
            context.Variables.Add("Octopus.Action.Azure.SubscriptionId", subscriptionId);
            context.Variables.Add("Octopus.Action.Azure.TenantId", tenantId);
            context.Variables.Add("Octopus.Action.Azure.ClientId", clientId);
            context.Variables.Add("Octopus.Action.Azure.Password", clientSecret);
        }
    }
}