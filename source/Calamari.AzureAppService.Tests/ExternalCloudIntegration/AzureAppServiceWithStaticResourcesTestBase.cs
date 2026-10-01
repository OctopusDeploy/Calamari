using System;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using NUnit.Framework;
using Octostache;

namespace Calamari.AzureAppService.Tests.ExternalCloudIntegration;

// Queries a pre-existing static resource group; creates and deletes nothing.
public abstract class AzureAppServiceWithStaticResourcesTestBase : AzureAppServiceTestBase
{
    //https://portal.azure.com/#@octopusdeploy.onmicrosoft.com/resource/subscriptions/cf21dc34-73dc-4d7d-bd86-041884e0bc75/resourceGroups/calamari-testing-static-rg/overview
    protected const string ResourceGroupName = "calamari-testing-static-rg";

    protected virtual string ResourceGroupLocation => "australiaeast";

    // Runs after the base AuthenticateWithAzure OneTimeSetUp, so ArmClient/SubscriptionResource are ready.
    [OneTimeSetUp]
    public async Task LookUpStaticResourceGroup()
    {
        await TestContext.Progress.WriteLineAsync($"Resource group location: {ResourceGroupLocation}");
        ResourceGroupResource = await SubscriptionResource.GetResourceGroupAsync(ResourceGroupName, CancellationToken);
    }

    protected override void AddAzureVariables(VariableDictionary variables)
    {
        base.AddAzureVariables(variables);
        variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, ResourceGroupName);
    }
}