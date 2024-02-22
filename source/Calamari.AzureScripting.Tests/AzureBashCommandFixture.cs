using System;
using System.Threading.Tasks;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Scripting;
using Calamari.Testing;
using Calamari.Testing.Requirements;
using Calamari.Testing.Tools;
using NUnit.Framework;

namespace Calamari.AzureScripting.Tests
{
    [TestFixture]
    class AzureBashCommandFixture : AzureScriptingFixtureBase
    {
        static IDeploymentTool AzureCLI = new InPathDeploymentTool("Octopus.Dependencies.AzureCLI", "AzureCLI\\wbin");
        
        void AddDefaults(CommandTestBuilderContext context)
        {
            context.Variables.Add(SpecialVariables.Account.AccountType, "AzureServicePrincipal");
            context.Variables.Add(SpecialVariables.Action.Azure.SubscriptionId, SubscriptionId);
            context.Variables.Add(SpecialVariables.Action.Azure.TenantId, TenantId);
            context.Variables.Add(SpecialVariables.Action.Azure.ClientId, ClientId);
            context.Variables.Add(SpecialVariables.Action.Azure.Password, ClientSecret);
            context.WithTool(AzureCLI);
        }
        
        [Test]
        [NonWindowsTest]
        public async Task ExecuteAnInlineBashScript()
        {
            var script = @"
#!/bin/bash
az --version
az group list";

            await CommandTestBuilder.CreateAsync<RunScriptCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(ScriptVariables.ScriptSource, ScriptVariables.ScriptSourceOptions.Inline);
                                                     context.Variables.Add(ScriptVariables.Syntax, ScriptSyntax.Bash.ToString());
                                                     context.Variables.Add(ScriptVariables.ScriptBody, script);
                                                 })
                                    .Execute();
        }
    }
}