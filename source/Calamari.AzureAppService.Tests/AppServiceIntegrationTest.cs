using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Calamari.Azure;
using Calamari.AzureAppService.Azure;
using Calamari.AzureAppService.Json;
using Calamari.CloudAccounts;
using Calamari.Testing;
using FluentAssertions;
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

        protected string Greeting = "Calamari";
        
        protected ArmClient ArmClient { get; private set; }
        protected ResourceGroupResource ResourceGroupResource { get; private set; }
        protected string ResourceGroupName => ResourceGroupResource.Data.Name;
        protected AppServicePlanResource LinuxAppServicePlanResource { get; private set; }

        protected AppServicePlanResource WindowsAppServicePlanResource { get; private set; }
        protected AppServicePlanResource WindowsContainerAppServicePlanResource { get; private set; }
        protected WebSiteResource WebSiteResource { get; private protected set; }

        readonly HttpClient client = new HttpClient();

        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        protected readonly CancellationToken CancellationToken = CancellationTokenSource.Token;
        SubscriptionResource subscriptionResource;

        [OneTimeSetUp]
        public async Task Setup()
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? DefaultVariables.ActiveDirectoryEndpoint;

            ClientId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, CancellationToken);
            ClientSecret = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, CancellationToken);
            TenantId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, CancellationToken);
            SubscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, CancellationToken);

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
            subscriptionResource = ArmClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(SubscriptionId));
            ResourceGroupResource staticResourceGroupResource = await subscriptionResource.GetResourceGroupAsync("calamari-testing-static-rg", CancellationToken);
            
            WindowsAppServicePlanResource = await staticResourceGroupResource.GetAppServicePlanAsync("calamari-testing-static-win-plan", CancellationToken);
            LinuxAppServicePlanResource = await staticResourceGroupResource.GetAppServicePlanAsync("calamari-testing-static-linux-plan", CancellationToken);
            WindowsContainerAppServicePlanResource = await staticResourceGroupResource.GetAppServicePlanAsync("calamari-testing-static-container-win-plan", CancellationToken);
            LinuxAppServicePlanResource = await staticResourceGroupResource.GetAppServicePlanAsync("calamari-testing-static-linux-plan", CancellationToken);
        }

        [SetUp]
        public virtual async Task SetUp()
        {
            var sw = Stopwatch.StartNew();
            var name = $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";
            
            TestContext.WriteLine($"Creating resource group '{name}'");
            
            var response = await subscriptionResource
                                 .GetResourceGroups()
                                 .CreateOrUpdateAsync(WaitUntil.Completed,
                                                      name,
                                                      new ResourceGroupData(AzureLocation.AustraliaEast)
                                                      {
                                                          Tags =
                                                          {
                                                              // give them an expiry of 14 days so if the tests fail to clean them up
                                                              // they will be automatically cleaned up by the Sandbox cleanup process
                                                              // We keep them for 14 days just in case we need to do debugging/investigation
                                                              ["LifetimeInDays"] = "14"
                                                          }
                                                      },
                                                      CancellationToken);

            ResourceGroupResource = response.Value;

            sw.Stop();
            TestContext.WriteLine($"Created resource group '{name}' in {sw.Elapsed:g}");
        }

        [TearDown]
        public virtual async Task TearDown()
        {
            var sw = Stopwatch.StartNew();
            TestContext.WriteLine($"Deleting web app '{WebSiteResource.Data.Name}'");
            
            //we explicitly delete the website so we can set deleteEmptyServerFarm to be false (otherwise cleaning up the resource group _sometimes_ deletes the app service plan)
            await WebSiteResource.DeleteAsync(WaitUntil.Completed, deleteEmptyServerFarm: false, cancellationToken: CancellationToken);
            sw.Stop();
            TestContext.WriteLine($"Deleted web app '{WebSiteResource.Data.Name}' in {sw.Elapsed:g}");
            
            TestContext.WriteLine($"Deleting resource group '{ResourceGroupResource.Data.Name}' (with no waiting)");
            //delete the rest of the resources
            await ResourceGroupResource.DeleteAsync(WaitUntil.Started, cancellationToken: CancellationToken);
        }

        protected async Task AssertContent(string hostName, string actualText, string rootPath = null)
        {
            var response = await RetryPolicies.TestsTransientHttpErrorsPolicy.ExecuteAsync(async (context, ct) =>
                                                                                           {
                                                                                               var r = await client.GetAsync($"https://{hostName}/{rootPath}", CancellationToken);
                                                                                               if (!r.IsSuccessStatusCode)
                                                                                               {
                                                                                                   var messageContent = await r.Content.ReadAsStringAsync();
                                                                                                   TestContext.WriteLine($"Unable to retrieve content from https://{hostName}/{rootPath}, failed with: {messageContent}");
                                                                                               }

                                                                                               r.EnsureSuccessStatusCode();
                                                                                               return r;
                                                                                           },
                                                                                           contextData: new Dictionary<string, object>(),
                                                                                           cancellationToken: CancellationToken);

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

        protected async Task<WebSiteResource> CreateWebApp(AppServicePlanResource appServicePlanResource, WebSiteData webSiteData = null)
        {
            var sw = Stopwatch.StartNew();
            
            webSiteData ??= new WebSiteData(ResourceGroupResource.Data.Location);
            webSiteData.AppServicePlanId = appServicePlanResource.Id;
            
            TestContext.WriteLine($"Creating web app '{ResourceGroupName}'");
            var webSiteResponse = await ResourceGroupResource.GetWebSites()
                                                             .CreateOrUpdateAsync(WaitUntil.Completed,
                                                                                  ResourceGroupName,
                                                                                  webSiteData,
                                                                                  CancellationToken);
            
            
            sw.Stop();
            TestContext.WriteLine($"Created web app '{ResourceGroupName}' in {sw.Elapsed:g}");

            return webSiteResponse.Value;
        }

        protected (string json, IEnumerable<AppSetting> setting) BuildAppSettingsJson(IEnumerable<(string name, string value, bool isSlotSetting)> settings)
        {
            var appSettings = settings.Select(setting => new AppSetting
                                                  { Name = setting.name, Value = setting.value, SlotSetting = setting.isSlotSetting });

            return (JsonConvert.SerializeObject(appSettings), appSettings);
        }
    }
}