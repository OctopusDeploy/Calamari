using System.Threading.Tasks;
using Calamari.AzureResourceGroup.Bicep;
using Calamari.Testing;
using Calamari.Testing.Azure;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests.ExternalCloudIntegration
{
    // Single real-cloud smoke test: a Bicep deploy round-trips against real Azure, and it is the only test that
    // exercises the real `az bicep build` compile (hence the Windows/az-cli requirements). Template-source
    // resolution and the compile -> substitute -> deploy wiring is covered without Azure or the az cli in
    // DeployBicepTemplateBehaviourUnitTestFixture.
    [TestFixture]
    [WindowsTest] // NOTE: We should look at having the Azure CLI installed on Linux boxes so that these steps can be tested there, particularly if we're moving cloud to a Ubuntu Default Worker.
    class DeployAzureBicepTemplateCommandFixture : AzureResourceGroupCloudTestBase
    {
        readonly string packagePath = TestEnvironment.GetTestPath("Packages", "Bicep");

        const string ParameterContent = """[{"Key":"storageAccountName","Value":"#{StorageAccountName}"},{"Key":"location","Value":"#{Location}"},{"Key":"sku","Value":"#{SKU}"}]""";

        [Test]
        [RequiresWindowsServer2016OrAbove("This test requires the az cli, which relies on python 3.10, which doesn't run on windows 2012/2012R2")]
        public async Task DeployAzureBicepTemplate_PackageSource()
        {
            await CommandTestBuilder.CreateAsync<DeployAzureBicepTemplateCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.TemplateSource, "Package");
                                                     context.Variables.Add(SpecialVariables.Action.Azure.BicepTemplate, "azure_website_template.bicep");
                                                     context.WithFilesToCopy(packagePath);
                                                 })
                                    .Execute();
        }

        void AddDefaults(CommandTestBuilderContext context)
        {
            context.Variables.Add(AzureScripting.SpecialVariables.Account.AccountType, "AzureServicePrincipal");
            context.Variables.Add(AzureAccountVariables.SubscriptionId, SubscriptionId);
            context.Variables.Add(AzureAccountVariables.TenantId, TenantId);
            context.Variables.Add(AzureAccountVariables.ClientId, ClientId);
            context.Variables.Add(AzureAccountVariables.Password, ClientSecret);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, ResourceGroupName);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupLocation, ResourceGroupLocation);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode, "Complete");
            context.Variables.Add(SpecialVariables.Action.Azure.BicepTemplateParameters, ParameterContent);

            context.Variables.Add("SKU", "Standard_LRS");
            context.Variables.Add("Location", ResourceGroupLocation);
            //storage accounts can be 24 chars long
            context.Variables.Add("StorageAccountName", AzureTestResourceHelpers.RandomName(length: 24));
        }
    }
}
