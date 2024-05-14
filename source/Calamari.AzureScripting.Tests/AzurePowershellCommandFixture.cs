using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Scripting;
using NUnit.Framework;
using Calamari.Testing;
using Calamari.Testing.Requirements;
using Calamari.Testing.Tools;

namespace Calamari.AzureScripting.Tests
{
    [TestFixture]
    class AzurePowerShellCommandFixture
    {
        string? clientId;
        string? clientSecret;
        string? tenantId;
        string? subscriptionId;
        
        static IDeploymentTool AzureCLI = new InPathDeploymentTool("Octopus.Dependencies.AzureCLI", "AzureCLI\\wbin");
        static IDeploymentTool AzureCmdlets = new BoostrapperModuleDeploymentTool("Octopus.Dependencies.AzureCmdlets",
                                                                                         new[]
                                                                                         {
                                                                                             "Powershell\\Azure.Storage\\4.6.1",
                                                                                             "Powershell\\Azure\\5.3.0",
                                                                                             "Powershell",
                                                                                         });
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;

        [OneTimeSetUp]
        public async Task Setup()
        {
            clientId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, cancellationToken);
            clientSecret = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, cancellationToken);
            tenantId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, cancellationToken);
            subscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, cancellationToken);
        }

        [Test]
        //[WindowsTest]
        [RequiresPowerShell5OrAbove]
        public async Task ExecuteAnInlineWindowsPowerShellScript()
        {
            var psScript = @"
$ErrorActionPreference = 'Continue'
az --version
Get-AzEnvironment
az group list";

            await CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                              .WithArrange(context =>
                                           {
                                               AddDefaults(context);
                                               context.Variables.Add(ScriptVariables.ScriptSource, ScriptVariables.ScriptSourceOptions.Inline);
                                               context.Variables.Add(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
                                               context.Variables.Add(ScriptVariables.ScriptBody, psScript);
                                               context.Variables.Add(KnownVariables.EnabledFeatureToggles, "AzureRMDeprecationFeatureToggle");
                                           })
                              .Execute();
        }

        [Test]
        [RequiresPowerShell5OrAbove]
        public async Task ExecuteAnInlinePowerShellCoreScript()
        {
            var psScript = @"
$ErrorActionPreference = 'Continue'
az --version
Get-AzureEnvironment
az group list";

            await CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
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
Get-AzureEnvironment
az group list";

            await CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                              .WithArrange(context =>
                                           {
                                               AddDefaults(context);
                                               context.Variables.Add(SpecialVariables.Action.Azure.Environment, "NotARealAzureEnvironment");
                                               context.Variables.Add(PowerShellVariables.Edition, ScriptVariables.ScriptSourceOptions.Core);
                                               context.Variables.Add(ScriptVariables.ScriptSource, ScriptVariables.ScriptSourceOptions.Inline);
                                               context.Variables.Add(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
                                               context.Variables.Add(ScriptVariables.ScriptBody, psScript);
                                           })
                              .Execute(false); // Should fail due to invalid azure environment
        }

        void AddDefaults(CommandTestBuilderContext context)
        {
            context.Variables.Add(SpecialVariables.Account.AccountType, "AzureServicePrincipal");
            context.Variables.Add(SpecialVariables.Action.Azure.SubscriptionId, subscriptionId);
            context.Variables.Add(SpecialVariables.Action.Azure.TenantId, tenantId);
            context.Variables.Add(SpecialVariables.Action.Azure.ClientId, clientId);
            context.Variables.Add(SpecialVariables.Action.Azure.Password, clientSecret);
            context.WithTool(AzureCLI);
            context.WithTool(AzureCmdlets);
        }
    }
}