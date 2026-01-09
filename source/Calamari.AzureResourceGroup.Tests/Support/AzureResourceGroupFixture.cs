using System;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Calamari.Azure;
using Calamari.CloudAccounts;
using Calamari.Testing;
using Calamari.Testing.Azure;

namespace Calamari.AzureResourceGroup.Tests.Support;

public class AzureResourceGroupFixture : IAsyncLifetime
{
    ArmClient armClient;
    SubscriptionResource subscriptionResource;

    public string ClientId { get; private set; }
    public string ClientSecret { get; private set; }
    public string SubscriptionId { get; private set; }
    public string TenantId { get; private set; }

    public string ResourceGroupName { get; private set; }
    public string ResourceGroupLocation { get; private set; }
    public ResourceGroupResource ResourceGroupResource { get; private set; }


    public async ValueTask InitializeAsync()
    {
        var resourceManagementEndpointBaseUri =
            Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? DefaultVariables.ResourceManagementEndpoint;
        var activeDirectoryEndpointBaseUri =
            Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? DefaultVariables.ActiveDirectoryEndpoint;

        ClientId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, TestContext.Current.CancellationToken);
        ClientSecret = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, TestContext.Current.CancellationToken);
        TenantId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, TestContext.Current.CancellationToken);
        SubscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, TestContext.Current.CancellationToken);

        ResourceGroupName = AzureTestResourceHelpers.GetResourceGroupName();

        ResourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? RandomAzureRegion.GetRandomRegionWithExclusions();

        var servicePrincipalAccount = new AzureServicePrincipalAccount(SubscriptionId,
                                                                       ClientId,
                                                                       TenantId,
                                                                       ClientSecret,
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

        //create the resource group
        subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(SubscriptionId));

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

    public async ValueTask DisposeAsync()
    {
        await armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(SubscriptionId, ResourceGroupResource.Data.Name))
                       .DeleteAsync(WaitUntil.Started, cancellationToken: TestContext.Current.CancellationToken);
    }
}

[CollectionDefinition(nameof(AzureResourceGroupFixture))]
public class AzureResourceGroupCollection : ICollectionFixture<AzureResourceGroupFixture>;