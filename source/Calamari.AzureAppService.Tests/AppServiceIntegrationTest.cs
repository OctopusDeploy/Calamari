using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Calamari.AzureAppService.Azure;
using Calamari.Testing;
using FluentAssertions;
using NUnit.Framework;
using Octostache;
using Polly;
using Polly.Retry;

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

        [OneTimeSetUp]
        public async Task Setup()
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? DefaultVariables.ActiveDirectoryEndpoint;

            //ask lawrence for the sandbox auto-cleanup tags
            ResourceGroupName = $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";

            ClientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            ClientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            TenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            SubscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            ResourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? "eastus";

            var servicePrincipalAccount = new ServicePrincipalAccount(
                                                                      SubscriptionId,
                                                                      ClientId,
                                                                      TenantId,
                                                                      ClientSecret,
                                                                      "AzurePublicCloud",
                                                                      resourceManagementEndpointBaseUri,
                                                                      activeDirectoryEndpointBaseUri);

            ArmClient = servicePrincipalAccount.CreateArmClient(retryOptions =>
                                                                {
                                                                    retryOptions.MaxRetries = 5;
                                                                    retryOptions.Mode = RetryMode.Exponential;
                                                                    retryOptions.Delay = TimeSpan.FromSeconds(2);
                                                                });

            //create the resource group
            SubscriptionResource = ArmClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(SubscriptionId));
            var response = await SubscriptionResource
                                 .GetResourceGroups()
                                 .CreateOrUpdateAsync(WaitUntil.Completed,
                                                      ResourceGroupName,
                                                      new ResourceGroupData(new AzureLocation(ResourceGroupLocation))
                                                      {
                                                          Tags =
                                                          {
                                                              // give them an expiry of 14 days so if the tests fail to clean them up
                                                              // they will be automatically cleaned up by the Sandbox cleanup process
                                                              // We keep them for 14 days just in case we need to do debugging/investigation
                                                              ["LifetimeInDays"] = "14"
                                                          }
                                                      });

            ResourceGroupResource = response.Value;

            await ConfigureTestResources(ResourceGroupResource);
        }

        protected abstract Task ConfigureTestResources(ResourceGroupResource resourceGroup);

        [OneTimeTearDown]
        public virtual async Task Cleanup()
        {
            await ArmClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(SubscriptionId, ResourceGroupName))
                           .DeleteAsync(WaitUntil.Started);
        }

        protected async Task AssertContent(string hostName, string actualText, string rootPath = null)
        {
            var response = await RetryPolicies.TransientHttpErrorsPolicy.ExecuteAsync(async () =>
                                                          {
                                                              var r = await client.GetAsync($"https://{hostName}/{rootPath}");
                                                              r.EnsureSuccessStatusCode();
                                                              return r;
                                                          });
            
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
            variables.Add("Octopus.Action.Azure.ResourceGroupName", ResourceGroupName);
            variables.Add("Octopus.Action.Azure.WebAppName", WebSiteResource.Data.Name);
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
                    Name = "S1",
                    Tier = "Standard"
                }
            };

            var servicePlanResponse = await resourceGroup.GetAppServicePlans()
                                                         .CreateOrUpdateAsync(WaitUntil.Completed,
                                                                              resourceGroup.Data.Name,
                                                                              appServicePlanData);

            webSiteData ??= new WebSiteData(resourceGroup.Data.Location);
            webSiteData.AppServicePlanId = servicePlanResponse.Value.Id;

            var webSiteResponse = await resourceGroup.GetWebSites()
                                                     .CreateOrUpdateAsync(WaitUntil.Completed,
                                                                          resourceGroup.Data.Name,
                                                                          webSiteData);

            return (servicePlanResponse.Value, webSiteResponse.Value);
        }
    }
}