using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Calamari.AzureAppService;
using Calamari.AzureAppService.Azure;
using Calamari.Testing;
using FluentAssertions;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Polly;
using Polly.Retry;

namespace Calamari.AzureAppService.Tests
{
    public abstract class LegacyAppServiceIntegrationTest
    {
        protected string clientId;
        protected string clientSecret;
        protected string tenantId;
        protected string subscriptionId;
        protected string resourceGroupName;
        protected string resourceGroupLocation;
        protected string greeting = "Calamari";
        protected string authToken;
        protected WebSiteManagementClient webMgmtClient;
        protected Site site;

        private ResourceGroupsOperations resourceGroupClient;
        private readonly HttpClient client = new HttpClient();

        protected RetryPolicy RetryPolicy { get; private set; }

        [OneTimeSetUp]
        public async Task Setup()
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? DefaultVariables.ActiveDirectoryEndpoint;

            resourceGroupName = $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";

            clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            resourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? "eastus";

            authToken = await Auth.GetAuthTokenAsync(tenantId,
                                                     clientId,
                                                     clientSecret,
                                                     resourceManagementEndpointBaseUri,
                                                     activeDirectoryEndpointBaseUri);

            var resourcesClient = new ResourcesManagementClient(subscriptionId,
                                                                new ClientSecretCredential(tenantId, clientId, clientSecret));

            resourceGroupClient = resourcesClient.ResourceGroups;

            var resourceGroup = new ResourceGroup(resourceGroupLocation)
            {
                Tags =
                {
                    // give them an expiry of 14 days so if the tests fail to clean them up
                    // they will be automatically cleaned up by the Sandbox cleanup process
                    // We keep them for 14 days just in case we need to do debugging/investigation
                    ["LifetimeInDays"] = "14"
                }
            };
            resourceGroup = await resourceGroupClient.CreateOrUpdateAsync(resourceGroupName, resourceGroup);

            webMgmtClient = new WebSiteManagementClient(new TokenCredentials(authToken))
            {
                SubscriptionId = subscriptionId,
                HttpClient = { BaseAddress = new Uri(DefaultVariables.ResourceManagementEndpoint) },
            };

            //Create a retry policy that retries on 429 errors. This is because we've been getting a number of flaky test failures
            RetryPolicy = RetryPolicyFactory.CreateForHttp429();

            await ConfigureTestResources(resourceGroup);
        }

        protected abstract Task ConfigureTestResources(ResourceGroup resourceGroup);

        [OneTimeTearDown]
        public async Task Cleanup()
        {
            if (resourceGroupClient != null)
                await resourceGroupClient.StartDeleteAsync(resourceGroupName);
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
            context.Variables.Add(AccountVariables.ClientId, clientId);
            context.Variables.Add(AccountVariables.Password, clientSecret);
            context.Variables.Add(AccountVariables.TenantId, tenantId);
            context.Variables.Add(AccountVariables.SubscriptionId, subscriptionId);
            context.Variables.Add("Octopus.Action.Azure.ResourceGroupName", resourceGroupName);
            context.Variables.Add("Octopus.Action.Azure.WebAppName", site.Name);
        }
    }
}