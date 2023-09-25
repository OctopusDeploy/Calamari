using System;
using System.Threading.Tasks;
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
    class AzurePowerShellCommandFixture : AzureScriptingFixtureBase 
    {
        
        static IDeploymentTool AzureCLI = new InPathDeploymentTool("Octopus.Dependencies.AzureCLI", "AzureCLI\\wbin");
        static IDeploymentTool AzureCmdlets = new BoostrapperModuleDeploymentTool("Octopus.Dependencies.AzureCmdlets",
                                                                                         new[]
                                                                                         {
                                                                                             "Powershell\\Azure.Storage\\4.6.1",
                                                                                             "Powershell\\Azure\\5.3.0",
                                                                                             "Powershell",
                                                                                         });

        [Test]
        [WindowsTest]
        [RequiresPowerShell5OrAbove]
        public async Task ExecuteAnInlineWindowsPowerShellScript()
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
                                               context.Variables.Add(ScriptVariables.ScriptSource, ScriptVariables.ScriptSourceOptions.Inline);
                                               context.Variables.Add(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
                                               context.Variables.Add(ScriptVariables.ScriptBody, psScript);
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
            context.Variables.Add(SpecialVariables.Action.Azure.SubscriptionId, SubscriptionId);
            context.Variables.Add(SpecialVariables.Action.Azure.TenantId, TenantId);
            context.Variables.Add(SpecialVariables.Action.Azure.ClientId, ClientId);
            context.Variables.Add(SpecialVariables.Action.Azure.Password, ClientSecret);
            context.WithTool(AzureCLI);
            context.WithTool(AzureCmdlets);
        }
    }
}