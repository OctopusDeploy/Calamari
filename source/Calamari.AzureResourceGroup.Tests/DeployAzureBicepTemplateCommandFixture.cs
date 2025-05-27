using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Calamari.Azure;
using Calamari.Azure.ResourceGroups;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Testing.Tools;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    class DeployAzureBicepTemplateCommandFixture
    {
        string clientId;
        string clientSecret;
        string tenantId;
        string subscriptionId;
        string resourceGroupName;
        string resourceGroupLocation;
        ArmClient armClient;
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;
        readonly string packagePath = TestEnvironment.GetTestPath("Packages", "Bicep");

        static IDeploymentTool AzureCLI = new InPathDeploymentTool("Octopus.Dependencies.AzureCLI", "AzureCLI\\wbin");

        [OneTimeSetUp]
        public async Task Setup()
        {
            clientId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, cancellationToken);
            clientSecret = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, cancellationToken);
            tenantId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, cancellationToken);
            subscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, cancellationToken);
            
            resourceGroupName = $"calamari-deploy-bicep-fixture-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            resourceGroupLocation = "australiasoutheast";
            
            armClient = new ArmClient(new ClientSecretCredential(tenantId, clientId, clientSecret), subscriptionId);
        }
        
        [OneTimeTearDown]
        public async Task Cleanup()
        {
            var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscriptionId));
            var resourceGroups = subscription.GetResourceGroups();
            var existing = await resourceGroups.GetIfExistsAsync(resourceGroupName, CancellationToken.None);
            if (existing.HasValue && existing.Value != null)
            {
                await existing.Value.DeleteAsync(WaitUntil.Started, cancellationToken: CancellationToken.None);
            }
        }

        [Test]
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

        [Test]
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

        [Test]
        public async Task DeployAzureBicepTemplate_InlineSource()
        {
            var templateFileContent = File.ReadAllText(Path.Combine(packagePath, "azure_website_template.bicep"));
            var paramsFileContent = File.ReadAllText(Path.Combine(packagePath, "parameters.json"));

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
            context.Variables.Add(AzureAccountVariables.SubscriptionId, subscriptionId);
            context.Variables.Add(AzureAccountVariables.TenantId, tenantId);
            context.Variables.Add(AzureAccountVariables.ClientId, clientId);
            context.Variables.Add(AzureAccountVariables.Password, clientSecret);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, resourceGroupName);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupLocation, resourceGroupLocation);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode, "Complete");
            context.Variables.Add(SpecialVariables.Action.Azure.TemplateParameters, "parameters.json");

            context.Variables.Add("SKU", "Standard_LRS");
            context.Variables.Add("Location", resourceGroupLocation);
            context.Variables.Add("StorageAccountName", "calamari" + Guid.NewGuid().ToString("N").Substring(0, 7));
        }

        static void AddTemplateFiles(CommandTestBuilderContext context, string template, string parameters)
        {
            context.WithDataFile(template, "template.bicep");
            context.WithDataFile(parameters, "parameters.json");
        }
    }
}