using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Calamari.Azure;
using Calamari.AzureAppService.Azure;
using Calamari.CloudAccounts;
using Calamari.Testing;
using NUnit.Framework;
using Octostache;
using AccountVariables = Calamari.AzureAppService.Azure.AccountVariables;

namespace Calamari.AzureAppService.Tests;

public abstract class AppServiceIntegrationTestWithStaticResources
{
    //https://portal.azure.com/#@octopusdeploy.onmicrosoft.com/resource/subscriptions/cf21dc34-73dc-4d7d-bd86-041884e0bc75/resourceGroups/calamari-testing-static-rg/overview
    protected const string ResourceGroupName = "calamari-testing-static-rg";

    static readonly CancellationTokenSource CancellationTokenSource = new();
    protected CancellationToken CancellationToken => CancellationTokenSource.Token;
    
    protected string ClientId { get; private set; }
    protected string ClientSecret { get; private set; }
    protected string TenantId { get; private set; }
    protected string SubscriptionId { get; private set; }
    protected ArmClient ArmClient { get; private set; }

    protected SubscriptionResource SubscriptionResource { get; private set; }
    protected ResourceGroupResource ResourceGroupResource { get; private set; }

    protected virtual string ResourceGroupLocation => "australiaeast";
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var resourceManagementEndpointBaseUri =
            Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? DefaultVariables.ResourceManagementEndpoint;
        var activeDirectoryEndpointBaseUri =
            Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? DefaultVariables.ActiveDirectoryEndpoint;

        ClientId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, CancellationToken);
        ClientSecret = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, CancellationToken);
        TenantId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, CancellationToken);
        SubscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, CancellationToken);

        await TestContext.Progress.WriteLineAsync($"Resource group location: {ResourceGroupLocation}");

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
                                                                // AzureAppServiceDeployContainerBehaviorFixture.AzureLinuxContainerSlotDeploy occasional timeout at default 100 seconds
                                                                retryOptions.NetworkTimeout = TimeSpan.FromSeconds(200);
                                                            });

        //create the resource group
        SubscriptionResource = ArmClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(SubscriptionId));
        ResourceGroupResource = await SubscriptionResource.GetResourceGroupAsync(ResourceGroupName, CancellationToken);
    }

    protected void AddAzureVariables(CommandTestBuilderContext context)
    {
        AddAzureVariables(context.Variables);
    }

    protected void AddAzureVariables(VariableDictionary variables)
    {
        variables.Add(AccountVariables.ClientId, ClientId);
        variables.Add(AccountVariables.Password, ClientSecret);
        variables.Add(AccountVariables.TenantId, TenantId);
        variables.Add(AccountVariables.SubscriptionId, SubscriptionId);
        variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, ResourceGroupName);
    }
}