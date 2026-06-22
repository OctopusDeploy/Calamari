using System;
using System.Threading.Tasks;
using Calamari.Testing;
using Calamari.Testing.Azure;
using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests.ExternalCloudIntegration
{
    // Single real-cloud smoke test: confirms an ARM template deploy round-trips against real Azure (auth +
    // ArmClient + ArmDeployments submit/poll). The template-source resolution (Package/GitRepository/Inline),
    // parameter normalisation and deployment mode/name logic is covered without Azure in
    // DeployAzureResourceGroupBehaviourUnitTestFixture via a mocked IAzureResourceGroupOperator. The Git source
    // takes the identical code path as Package, so it needs no separate cloud test; the former az-cli
    // "tools are configured" test exercised the runner environment, not Calamari, and was removed.
    [TestFixture]
    class AzureResourceGroupActionHandlerFixture : AzureResourceGroupCloudTestBase
    {
        [Test]
        public async Task Deploy_with_template_in_package()
        {
            var packagePath = TestEnvironment.GetTestPath("Packages", "AzureResourceGroup");
            await CommandTestBuilder.CreateAsync<DeployAzureResourceGroupCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode, "Complete");
                                                     context.Variables.Add("Octopus.Action.Azure.TemplateSource", "Package");
                                                     context.Variables.Add("Octopus.Action.Azure.ResourceGroupTemplate", "azure_website_template.json");
                                                     context.Variables.Add("Octopus.Action.Azure.ResourceGroupTemplateParameters", "azure_website_params.json");
                                                     context.WithFilesToCopy(packagePath);
                                                 })
                                    .Execute();
        }

        void AddDefaults(CommandTestBuilderContext context)
        {
            context.Variables.Add("Octopus.Account.AccountType", "AzureServicePrincipal");
            context.Variables.Add(AzureAccountVariables.SubscriptionId, SubscriptionId);
            context.Variables.Add(AzureAccountVariables.TenantId, TenantId);
            context.Variables.Add(AzureAccountVariables.ClientId, ClientId);
            context.Variables.Add(AzureAccountVariables.Password, ClientSecret);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, ResourceGroupName);
            context.Variables.Add("ResourceGroup", ResourceGroupName);
            context.Variables.Add("SKU", "Shared");
            //as we have a single resource group, we need to have unique web app name per test
            context.Variables.Add("WebSite", $"Calamari-{Guid.NewGuid():N}");
            context.Variables.Add("Location", ResourceGroupResource.Data.Location);
            //this is a storage account prefix, so just make it as random as possible
            //The names of the storage accounts are a max of 7 chars, so we generate a prefix of 17 chars (storage accounts have a max of 24)
            context.Variables.Add("AccountPrefix", AzureTestResourceHelpers.RandomName(length: 17));
        }
    }
}
