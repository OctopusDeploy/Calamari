using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.ExternalTools.Tests.Infrastructure;
using Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies;
using Calamari.Scripting;
using Calamari.Testing;
using Calamari.Testing.Requirements;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.AzureCli
{
    /// <summary>
    /// Azure PowerShell scripting tests — require Azure CLI and PowerShell Az module.
    /// Validates that Calamari's Azure scripting context correctly sets up az CLI auth.
    /// Migrated from Calamari.AzureScripting.Tests/AzurePowerShellCommandFixture.
    /// </summary>
    [TestFixture]
    public class AzurePowerShellScriptingFixture : ExternalToolFixture
    {
        protected override string PrimaryToolName => "azure-cli";

        protected override Task<string> DownloadTool(string destinationDir, string version, HttpClient client)
            => AzureCliStrategy.Download(destinationDir, version, client);

        string clientId;
        string clientSecret;
        string tenantId;
        string subscriptionId;

        static readonly CancellationTokenSource CancellationTokenSource = new();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;

        [OneTimeSetUp]
        public async Task SetupAzure()
        {
            clientId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, cancellationToken);
            clientSecret = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, cancellationToken);
            tenantId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, cancellationToken);
            subscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, cancellationToken);
        }

        [Test]
        [RequiresPowerShell5OrAbove]
        public async Task ExecuteAnInlinePowerShellCoreScript()
        {
            var psScript = @"
$ErrorActionPreference = 'Continue'
az --version
Get-AzEnvironment
az group list";

            await CommandTestBuilder.CreateAsync<RunScriptCommand, AzureScripting.Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context);
                    context.Variables.Add(PowerShellVariables.Edition, ScriptVariables.ScriptSourceOptions.Core);
                    context.Variables.Add(ScriptVariables.ScriptSource, ScriptVariables.ScriptSourceOptions.Inline);
                    context.Variables.Add(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
                    context.Variables.Add(ScriptVariables.ScriptBody, psScript);
                })
                .Execute();
        }

        [Test]
        [RequiresPowerShell5OrAbove]
        public async Task ExecuteAnInlinePowerShellCoreScriptAgainstAnInvalidAzureEnvironment()
        {
            var psScript = @"
$ErrorActionPreference = 'Continue'
az --version
Get-AzEnvironment
az group list";

            await CommandTestBuilder.CreateAsync<RunScriptCommand, AzureScripting.Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context);
                    context.Variables.Set(AzureScripting.SpecialVariables.Action.Azure.Environment, "NotARealAzureEnvironment");
                    context.Variables.Add(PowerShellVariables.Edition, ScriptVariables.ScriptSourceOptions.Core);
                    context.Variables.Add(ScriptVariables.ScriptSource, ScriptVariables.ScriptSourceOptions.Inline);
                    context.Variables.Add(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
                    context.Variables.Add(ScriptVariables.ScriptBody, psScript);
                })
                .Execute(false); // Should fail due to invalid azure environment
        }

        void AddDefaults(CommandTestBuilderContext context)
        {
            context.Variables.Add(AzureScripting.SpecialVariables.Action.Azure.Environment, "AzureCloud");
            context.Variables.Add(AzureScripting.SpecialVariables.Account.AccountType, "AzureServicePrincipal");
            context.Variables.Add(AzureScripting.SpecialVariables.Action.Azure.SubscriptionId, subscriptionId);
            context.Variables.Add(AzureScripting.SpecialVariables.Action.Azure.TenantId, tenantId);
            context.Variables.Add(AzureScripting.SpecialVariables.Action.Azure.ClientId, clientId);
            context.Variables.Add(AzureScripting.SpecialVariables.Action.Azure.Password, clientSecret);
        }
    }
}
