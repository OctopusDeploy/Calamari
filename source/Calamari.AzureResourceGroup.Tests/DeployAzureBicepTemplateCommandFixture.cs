using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Testing.Tools;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests
{
    [TestFixture]
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
        public async Task DeployAzureBicepTemplate_Package()
        {
            await CommandTestBuilder.CreateAsync<DeployAzureBicepTemplateCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode, "Complete");
                                                     context.Variables.Add(SpecialVariables.Action.Azure.TemplateSource, "Package");
                                                     context.Variables.Add(SpecialVariables.Action.Azure.BicepTemplate, "azure_website_template.bicep");
                                                     context.Variables.Add(SpecialVariables.Action.Azure.TemplateParameters, "parameters.json");
                                                     context.WithFilesToCopy(packagePath);
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
            context.Variables.Add("SKU", "Standard_LRS");
            context.Variables.Add("Location", resourceGroupLocation);
            context.Variables.Add("StorageAccountName", "calamari" + Guid.NewGuid().ToString("N").Substring(0, 7));
        }
    }
}