using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Calamari.Azure;
using Calamari.AzureResourceGroup.Bicep;
using Calamari.CloudAccounts;
using AzureRgSpecialVariables = Calamari.AzureResourceGroup.SpecialVariables;
using AzureRgAccountVariables = Calamari.AzureResourceGroup.AzureAccountVariables;
using Calamari.ExternalTools.Tests.Infrastructure;
using Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies;
using Calamari.Testing;
using Calamari.Testing.Azure;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.AzureCli
{
    /// <summary>
    /// Bicep template deployment tests — require Azure CLI (az bicep).
    /// Migrated from Calamari.AzureResourceGroup.Tests/DeployAzureBicepTemplateCommandFixture.
    /// </summary>
    [TestFixture]
    [WindowsTest]
    public class DeployAzureBicepTemplateFixture : ExternalToolFixture
    {
        protected override string PrimaryToolName => "azure-cli";

        protected override Task<string> DownloadTool(string destinationDir, string version, HttpClient client)
            => AzureCliStrategy.Download(destinationDir, version, client);

        string clientId;
        string clientSecret;
        string tenantId;
        string subscriptionId;
        string resourceGroupName;
        string resourceGroupLocation;
        ArmClient armClient;

        static readonly CancellationTokenSource CancellationTokenSource = new(TimeSpan.FromMinutes(5));
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;

        string PackagePath => Path.Combine(TestEnvironment.CurrentWorkingDirectory, "AzureCli", "Packages", "Bicep");

        const string ParameterContent = """[{"Key":"storageAccountName","Value":"#{StorageAccountName}"},{"Key":"location","Value":"#{Location}"},{"Key":"sku","Value":"#{SKU}"}]""";

        [OneTimeSetUp]
        public async Task SetupAzure()
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? "https://management.azure.com/";
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? "https://login.windows.net/";

            clientId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, cancellationToken);
            clientSecret = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, cancellationToken);
            tenantId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, cancellationToken);
            subscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, cancellationToken);

            resourceGroupName = AzureTestResourceHelpers.GetResourceGroupName();
            resourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? RandomAzureRegion.GetRandomRegionWithExclusions();

            var servicePrincipalAccount = new AzureServicePrincipalAccount(subscriptionId,
                clientId, tenantId, clientSecret,
                "AzureGlobalCloud",
                resourceManagementEndpointBaseUri,
                activeDirectoryEndpointBaseUri);

            armClient = servicePrincipalAccount.CreateArmClient(retryOptions =>
            {
                retryOptions.MaxRetries = 5;
                retryOptions.Mode = RetryMode.Exponential;
                retryOptions.Delay = TimeSpan.FromSeconds(2);
                retryOptions.NetworkTimeout = TimeSpan.FromSeconds(200);
            });

            var subscriptionResource = armClient.GetSubscriptionResource(
                SubscriptionResource.CreateResourceIdentifier(subscriptionId));

            await subscriptionResource
                .GetResourceGroups()
                .CreateOrUpdateAsync(WaitUntil.Completed,
                    resourceGroupName,
                    new ResourceGroupData(new AzureLocation(resourceGroupLocation))
                    {
                        Tags =
                        {
                            [AzureTestResourceHelpers.ResourceGroupTags.LifetimeInDaysKey] = AzureTestResourceHelpers.ResourceGroupTags.LifetimeInDaysValue,
                            [AzureTestResourceHelpers.ResourceGroupTags.SourceKey] = AzureTestResourceHelpers.ResourceGroupTags.SourceValue
                        }
                    });
        }

        [OneTimeTearDown]
        public async Task Cleanup()
        {
            if (armClient != null)
            {
                await armClient.GetResourceGroupResource(
                        ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName))
                    .DeleteAsync(WaitUntil.Started);
            }
        }

        [Test]
        [RequiresWindowsServer2016OrAbove("This test requires the az cli, which relies on python 3.10")]
        public async Task DeployAzureBicepTemplate_PackageSource()
        {
            await CommandTestBuilder.CreateAsync<DeployAzureBicepTemplateCommand, AzureResourceGroup.Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context);
                    context.Variables.Add(AzureRgSpecialVariables.Action.Azure.TemplateSource, "Package");
                    context.Variables.Add(AzureRgSpecialVariables.Action.Azure.BicepTemplate, "azure_website_template.bicep");
                    context.WithFilesToCopy(PackagePath);
                })
                .Execute();
        }

        [Test]
        [RequiresWindowsServer2016OrAbove("This test requires the az cli, which relies on python 3.10")]
        public async Task DeployAzureBicepTemplate_InlineSource()
        {
            var templateFileContent = File.ReadAllText(Path.Combine(PackagePath, "azure_website_template.bicep"));

            await CommandTestBuilder.CreateAsync<DeployAzureBicepTemplateCommand, AzureResourceGroup.Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context);
                    context.Variables.Add(AzureRgSpecialVariables.Action.Azure.ResourceGroupDeploymentMode, "Complete");
                    context.Variables.Add(AzureRgSpecialVariables.Action.Azure.TemplateSource, "Inline");
                    context.WithDataFile(templateFileContent, "template.bicep");
                })
                .Execute();
        }

        void AddDefaults(CommandTestBuilderContext context)
        {
            context.Variables.Add(AzureScripting.SpecialVariables.Account.AccountType, "AzureServicePrincipal");
            context.Variables.Add(AzureRgAccountVariables.SubscriptionId, subscriptionId);
            context.Variables.Add(AzureRgAccountVariables.TenantId, tenantId);
            context.Variables.Add(AzureRgAccountVariables.ClientId, clientId);
            context.Variables.Add(AzureRgAccountVariables.Password, clientSecret);
            context.Variables.Add(AzureRgSpecialVariables.Action.Azure.ResourceGroupName, resourceGroupName);
            context.Variables.Add(AzureRgSpecialVariables.Action.Azure.ResourceGroupLocation, resourceGroupLocation);
            context.Variables.Add(AzureRgSpecialVariables.Action.Azure.ResourceGroupDeploymentMode, "Complete");
            context.Variables.Add(AzureRgSpecialVariables.Action.Azure.BicepTemplateParameters, ParameterContent);
            context.Variables.Add("SKU", "Standard_LRS");
            context.Variables.Add("Location", resourceGroupLocation);
            context.Variables.Add("StorageAccountName", AzureTestResourceHelpers.RandomName(length: 24));
        }
    }
}
