using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Calamari.Azure;
using Calamari.Azure.AppServices;
using Calamari.AzureAppService.Azure;
using Calamari.CloudAccounts;
using Calamari.Testing;
using Calamari.Testing.Azure;
using FluentAssertions;
using JetBrains.TeamCity.ServiceMessages.Write.Special.Impl.Writer;
using Newtonsoft.Json;
using NUnit.Framework;
using Octostache;
using AccountVariables = Calamari.AzureAppService.Azure.AccountVariables;

namespace Calamari.AzureAppService.Tests
{
    public abstract class AppServiceIntegrationTest
    {
        protected string ClientId { get; private set; }
        protected string ClientSecret { get; private set; }
        protected string TenantId { get; private set; }
        protected string SubscriptionId { get; private set; }
        protected string ResourceGroupName { get; private set; }
        protected string ResourceGroupLocation { get; private set; }

        protected string greeting = "Calamari";
        protected ArmClient ArmClient { get; private set; }

        protected SubscriptionResource SubscriptionResource { get; private set; }
        protected ResourceGroupResource ResourceGroupResource { get; private set; }
        protected WebSiteResource WebSiteResource { get; private protected set; }

        private readonly HttpClient client = new HttpClient();

        /// <summary>
        /// Regions this fixture must never run in (e.g. because they lack a required SKU or feature).
        /// Excluded from both the initial region choice and any capacity-driven retries.
        /// </summary>
        protected virtual string[] ExcludedRegions => Array.Empty<string>();

        // App Service occasionally returns 409 (Conflict) when a region is out of capacity; retry in a
        // different region up to this many times before giving up.
        const int MaxRegionAttempts = 3;

        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;

        [OneTimeSetUp]
        public async Task Setup()
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? DefaultVariables.ActiveDirectoryEndpoint;

            ClientId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, cancellationToken);
            ClientSecret = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, cancellationToken);
            TenantId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, cancellationToken);
            SubscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, cancellationToken);

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

            SubscriptionResource = ArmClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(SubscriptionId));

            // An explicitly requested region is honoured verbatim: don't rotate away from it on a capacity conflict.
            var explicitRegion = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION");

            var triedRegions = new List<string>();

            while (true)
            {
                // A fresh resource group name per attempt: a resource group's location is immutable, so a
                // region switch needs a new group rather than reusing the one that hit the capacity conflict.
                ResourceGroupName = AzureTestResourceHelpers.GetResourceGroupName();
                ResourceGroupLocation = !string.IsNullOrEmpty(explicitRegion)
                    ? explicitRegion
                    : RandomAzureRegion.GetRandomRegionWithExclusions(ExcludedRegions.Concat(triedRegions).ToArray());
                triedRegions.Add(ResourceGroupLocation);

                TestContext.Progress.WriteLine($"Resource group location: {ResourceGroupLocation}");

                try
                {
                    //create the resource group
                    var response = await SubscriptionResource
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

                    await ConfigureTestResources(ResourceGroupResource);

                    return;
                }
                catch (Exception ex) when (IsCapacityConflict(ex) && string.IsNullOrEmpty(explicitRegion) && triedRegions.Count < MaxRegionAttempts)
                {
                    TestContext.Progress.WriteLine($"App Service reported a capacity conflict (409) in region '{ResourceGroupLocation}'. Retrying in a different region. Details: {ex.Message}");
                    await TryCleanupResourceGroup(ResourceGroupName);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Setup failed in region: {ResourceGroupLocation}", ex);
                }
            }
        }

        // App Service surfaces an out-of-capacity condition as a 409 (Conflict); walk any wrapping/aggregate
        // exceptions so we recognise it wherever it originates.
        static bool IsCapacityConflict(Exception ex) =>
            ex switch
            {
                RequestFailedException { Status: 409 } => true,
                AggregateException aggregate => aggregate.InnerExceptions.Any(IsCapacityConflict),
                _ => ex.InnerException != null && IsCapacityConflict(ex.InnerException)
            };

        async Task TryCleanupResourceGroup(string resourceGroupName)
        {
            try
            {
                await ArmClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(SubscriptionId, resourceGroupName))
                               .DeleteAsync(WaitUntil.Started, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                // Best effort: a leftover group is reaped by the LifetimeInDays tag, so don't fail setup over cleanup.
                await TestContext.Progress.WriteLineAsync($"Failed to clean up resource group '{resourceGroupName}' after a capacity conflict: {ex.Message}");
            }
        }

        protected abstract Task ConfigureTestResources(ResourceGroupResource resourceGroup);

        [OneTimeTearDown]
        public virtual async Task Cleanup()
        {
            await ArmClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(SubscriptionId, ResourceGroupName))
                           .DeleteAsync(WaitUntil.Started, cancellationToken: cancellationToken);
        }

        protected async Task AssertContent(string hostName, string actualText, string rootPath = null)
        {
            var response = await RetryPolicies.TestsTransientHttpErrorsPolicy.ExecuteAsync(async context =>
                                                                                           {
                                                                                               var r = await client.GetAsync($"https://{hostName}/{rootPath}");
                                                                                               if (!r.IsSuccessStatusCode)
                                                                                               {
                                                                                                   var messageContent = await r.Content.ReadAsStringAsync();
                                                                                                   TestContext.WriteLine($"Unable to retrieve content from https://{hostName}/{rootPath}, failed with: {messageContent}");
                                                                                               }

                                                                                               r.EnsureSuccessStatusCode();
                                                                                               return r;
                                                                                           },
                                                                                           contextData: new Dictionary<string, object>());

            var result = await response.Content.ReadAsStringAsync();
            result.Should().Contain(actualText);
        }

        protected static async Task DoWithRetries(int retries, Func<Task> action, int secondsBetweenRetries)
        {
            foreach (var retry in Enumerable.Range(1, retries))
            {
                try
                {
                    await action();
                    break;
                }
                catch
                {
                    if (retry == retries)
                        throw;

                    await Task.Delay(secondsBetweenRetries * 1000);
                }
            }
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
            variables.Add(SpecialVariables.Action.Azure.WebAppName, WebSiteResource.Data.Name);
        }

        protected async Task<(AppServicePlanResource, WebSiteResource)> CreateAppServicePlanAndWebApp(
            ResourceGroupResource resourceGroup,
            AppServicePlanData appServicePlanData = null,
            WebSiteData webSiteData = null)
        {
            appServicePlanData ??= new AppServicePlanData(resourceGroup.Data.Location)
            {
                Sku = new AppServiceSkuDescription
                {
                    Name = "P1V3",
                    Tier = "PremiumV3"
                }
            };

            var servicePlanResponse = await resourceGroup.GetAppServicePlans()
                                                         .CreateOrUpdateAsync(WaitUntil.Completed,
                                                                              resourceGroup.Data.Name,
                                                                              appServicePlanData);

            webSiteData ??= new WebSiteData(resourceGroup.Data.Location);
            webSiteData.AppServicePlanId = servicePlanResponse.Value.Id;

            //this may have been set already
            webSiteData.SiteConfig ??= new SiteConfigProperties
            {
                //use .NET 8.0
                NetFrameworkVersion = "v8.0",
                Use32BitWorkerProcess = false
            };

            var webSiteResponse = await resourceGroup.GetWebSites()
                                                     .CreateOrUpdateAsync(WaitUntil.Completed,
                                                                          resourceGroup.Data.Name,
                                                                          webSiteData);

            return (servicePlanResponse.Value, webSiteResponse.Value);
        }

        protected (string json, IEnumerable<AppSetting> setting) BuildAppSettingsJson(IEnumerable<(string name, string value, bool isSlotSetting)> settings)
        {
            var appSettings = settings.Select(setting => new AppSetting
                                                  { Name = setting.name, Value = setting.value, SlotSetting = setting.isSlotSetting });

            return (JsonConvert.SerializeObject(appSettings), appSettings);
        }
    }
}