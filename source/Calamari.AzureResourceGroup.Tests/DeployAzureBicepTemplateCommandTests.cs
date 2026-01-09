using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.AzureResourceGroup.Tests.Attributes;
using Calamari.AzureResourceGroup.Tests.Support;
using Calamari.Testing;
using Calamari.Testing.Azure;
using Calamari.Testing.Helpers;
using Calamari.Testing.Tools;

namespace Calamari.AzureResourceGroup.Tests
{
    [TestPlatforms(TestCategory.CompatibleOS.OnlyWindows)]
    [Collection(nameof(AzureResourceGroupFixture))]
    public class DeployAzureBicepTemplateCommandTests(AzureResourceGroupFixture resourceGroupFixture) : CalamariTest
    {
        readonly AzureResourceGroupFixture resourceGroupFixture = resourceGroupFixture;
        
        readonly string packagePath = TestEnvironment.GetTestPath("Packages", "Bicep");

        static IDeploymentTool AzureCLI = new InPathDeploymentTool("Octopus.Dependencies.AzureCLI", "AzureCLI\\wbin");

        protected override TimeSpan TestTimeout => TimeSpan.FromMinutes(5);

        [Fact]
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

        [Fact]
        public async Task DeployAzureBicepTemplate_GitSource()
        {
            // For the purposes of Bicep templates in Calamari, a template in a Git Repository
            // is equivalent to a template in a package, so we can just re-use the same
            // package in the test here, it's just the template source property that is
            // different.
            await CommandTestBuilder.CreateAsync<DeployAzureBicepTemplateCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.TemplateSource, "GitRepository");
                                                     context.Variables.Add(SpecialVariables.Action.Azure.BicepTemplate, "azure_website_template.bicep");
                                                     context.WithFilesToCopy(packagePath);
                                                 })
                                    .Execute();
        }

        [Fact]
        public async Task DeployAzureBicepTemplate_InlineSource()
        {
            var templateFileContent = await File.ReadAllTextAsync(Path.Combine(packagePath, "azure_website_template.bicep"), CancellationToken);
            var paramsFileContent = await File.ReadAllTextAsync(Path.Combine(packagePath, "parameters.json"), CancellationToken);

            await CommandTestBuilder.CreateAsync<DeployAzureBicepTemplateCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode, "Complete");
                                                     context.Variables.Add(SpecialVariables.Action.Azure.TemplateSource, "Inline");
                                                     AddTemplateFiles(context, templateFileContent, paramsFileContent);
                                                 })
                                    .Execute();
        }

        void AddDefaults(CommandTestBuilderContext context)
        {
            context.WithTool(AzureCLI);

            context.Variables.Add(AzureScripting.SpecialVariables.Account.AccountType, "AzureServicePrincipal");
            context.Variables.Add(AzureAccountVariables.SubscriptionId, resourceGroupFixture.SubscriptionId);
            context.Variables.Add(AzureAccountVariables.TenantId, resourceGroupFixture.TenantId);
            context.Variables.Add(AzureAccountVariables.ClientId, resourceGroupFixture.ClientId);
            context.Variables.Add(AzureAccountVariables.Password, resourceGroupFixture.ClientSecret);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, resourceGroupFixture.ResourceGroupName);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupLocation, resourceGroupFixture.ResourceGroupLocation);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode, "Complete");
            context.Variables.Add(SpecialVariables.Action.Azure.TemplateParameters, "parameters.json");

            context.Variables.Add("SKU", "Standard_LRS");
            context.Variables.Add("Location", resourceGroupFixture.ResourceGroupLocation);
            //storage accounts can be 24 chars long
            context.Variables.Add("StorageAccountName", AzureTestResourceHelpers.RandomName(length: 24));
        }

        static void AddTemplateFiles(CommandTestBuilderContext context, string template, string parameters)
        {
            context.WithDataFile(template, "template.bicep");
            context.WithDataFile(parameters, "parameters.json");
        }
    }
}