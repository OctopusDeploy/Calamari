#if AZURE_CORE
using System.Collections.Generic;
using Calamari.Azure.Integration;
using Calamari.Deployment;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Tests.Fixtures;
using Calamari.Tests.Helpers;
using Calamari.Variables;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AzureFixtures
{
    [TestFixture]
    public class AzurePowerShellContextFixture : CalamariFixture
    {
        static readonly string AzureSubscriptId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
        static readonly string AzureTenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
        static readonly string AzureClientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
        static readonly string AzurePassword = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
        
        [Test]
        [TestCase("Azure", "", ScriptSyntax.PowerShell, true)]
        [TestCase("Nope", "", ScriptSyntax.PowerShell, false)]
        [TestCase("Azure", "Nope", ScriptSyntax.PowerShell, false)]
        [TestCase("Azure", "", ScriptSyntax.FSharp, false)]
        public void ShouldBeEnabled(string accountType, string connectionEndpoint, ScriptSyntax syntax, bool expected)
        {
            var variables = new CalamariVariables
            {
                {SpecialVariables.Account.AccountType, accountType},
                {SpecialVariables.Action.ServiceFabric.ConnectionEndpoint, connectionEndpoint}
            };
            var target = new AzurePowerShellContext(variables);
            var actual = target.IsEnabled(syntax);
            actual.Should().Be(expected);
        }
        
        [Test]
        [TestCase("")]
        [TestCase("C:\\Azure\\Modules")]
        [TestCase("/etc/azure/modules")]
        [RequiresPowerShell5OrAbove]
        public void AzureExtension(string extensionsDirectory)
        {
            var variables = new Dictionary<string, string>()
            {
                {SpecialVariables.Account.AccountType, "AzureServicePrincipal"},
                {SpecialVariables.Action.Azure.TenantId, AzureTenantId},
                {SpecialVariables.Action.Azure.ClientId, AzureClientId},
                {SpecialVariables.Action.Azure.Password, AzurePassword},
                {SpecialVariables.Action.Azure.SubscriptionId, AzureSubscriptId},
                {SpecialVariables.Action.Azure.ExtensionsDirectory, extensionsDirectory},
            };
            
            (CalamariResult result, _) = RunScript("azExtensionsModuleFolder.ps1", variables, extensions: new List<string>() {"Calamari.Azure"});

            result.AssertSuccess();
        }
    }
}
#endif