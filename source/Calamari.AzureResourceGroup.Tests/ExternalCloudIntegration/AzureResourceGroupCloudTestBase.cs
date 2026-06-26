using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Calamari.Azure;
using Calamari.CloudAccounts;
using Calamari.Testing;
using Calamari.Testing.Azure;
using Calamari.Testing.Helpers;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests.ExternalCloudIntegration
{
    // Shared base for the real-cloud resource-group smoke tests: authenticates with the test service principal,
    // provisions a fresh resource group, and deletes it on teardown. Derived fixtures inherit the
    // ExternalCloudIntegration category.
    [Category(TestCategory.ExternalCloudIntegration)]
    abstract class AzureResourceGroupCloudTestBase
    {
        protected string ClientId { get; private set; }
        protected string ClientSecret { get; private set; }
        protected string TenantId { get; private set; }
        protected string SubscriptionId { get; private set; }
        protected string ResourceGroupName { get; private set; }
        protected string ResourceGroupLocation { get; private set; }
        protected ArmClient ArmClient { get; private set; }
        protected ResourceGroupResource ResourceGroupResource { get; private set; }

        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;

        [OneTimeSetUp]
        public async Task CreateResourceGroup()
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? DefaultVariables.ActiveDirectoryEndpoint;

            ClientId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, cancellationToken);
            ClientSecret = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, cancellationToken);
            TenantId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, cancellationToken);
            SubscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, cancellationToken);

            ResourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? RandomAzureRegion.GetRandomRegionWithExclusions();
            ResourceGroupName = AzureTestResourceHelpers.GetResourceGroupName();

            var servicePrincipalAccount = new AzureServicePrincipalAccount(SubscriptionId,
                                                                           ClientId,
                                                                           TenantId,
                                                                           ClientSecret,
                                                                           "AzureGlobalCloud",
                                                                           resourceManagementEndpointBaseUri,
                                                                           activeDirectoryEndpointBaseUri);

            ArmClient = servicePrincipalAccount.CreateArmClient(retryOptions =>
                                                                {
                                                                    retryOptions.MaxRetries = 5;
                                                                    retryOptions.Mode = RetryMode.Exponential;
                                                                    retryOptions.Delay = TimeSpan.FromSeconds(2);
                                                                    retryOptions.NetworkTimeout = TimeSpan.FromSeconds(200);
                                                                });

            var subscriptionResource = ArmClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(SubscriptionId));

            var response = await subscriptionResource
                                 .GetResourceGroups()
                                 .CreateOrUpdateAsync(WaitUntil.Completed,
                                                      ResourceGroupName,
                                                      new ResourceGroupData(new AzureLocation(ResourceGroupLocation))
                                                      {
                                                          Tags =
                                                          {
                                                              [AzureTestResourceHelpers.ResourceGroupTags.LifetimeInDaysKey] = AzureTestResourceHelpers.ResourceGroupTags.LifetimeInDaysValue,
                                                              [AzureTestResourceHelpers.ResourceGroupTags.SourceKey] = AzureTestResourceHelpers.ResourceGroupTags.SourceValue
                                                          }
                                                      });

            ResourceGroupResource = response.Value;
        }

        [OneTimeTearDown]
        public async Task DeleteResourceGroup()
        {
            await ArmClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(SubscriptionId, ResourceGroupName))
                           .DeleteAsync(WaitUntil.Started);
        }
    }
}
