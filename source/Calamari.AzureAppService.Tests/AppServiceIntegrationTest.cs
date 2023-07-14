using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
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
    public abstract class AppServiceIntegrationTest
    {
        SubscriptionResource subscriptionResource;
        protected string clientId;
        protected string clientSecret;
        protected string tenantId;
        protected string subscriptionId;
        protected string resourceGroupName;
        protected string resourceGroupLocation;
        protected string greeting = "Calamari";
        protected string authToken;
        protected ArmClient ArmClient { get; private set; }

        private readonly HttpClient client = new HttpClient();

        protected RetryPolicy RetryPolicy { get; private set; }

        [OneTimeSetUp]
        public async Task Setup()
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? DefaultVariables.ActiveDirectoryEndpoint;

            resourceGroupName = Randomizer.CreateRandomizer().GetString(34, "abcdefghijklmnopqrstuvwxyz1234567890");

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

            var servicePrincipalAccount = new ServicePrincipalAccount(
                                                                      subscriptionId,
                                                                      clientId,
                                                                      tenantId,
                                                                      clientSecret,
                                                                      ArmEnvironment.AzurePublicCloud.ToString(),
                                                                      resourceManagementEndpointBaseUri,
                                                                      activeDirectoryEndpointBaseUri);

            ArmClient = servicePrincipalAccount.CreateArmClient(retryOptions =>
                                                                {
                                                                    retryOptions.MaxRetries = 5;
                                                                    retryOptions.Mode = RetryMode.Exponential;
                                                                    retryOptions.Delay = TimeSpan.FromSeconds(2);
                                                                });

            //create the resource group
            subscriptionResource = ArmClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscriptionId));
            var response = await subscriptionResource
                                 .GetResourceGroups()
                                 .CreateOrUpdateAsync(WaitUntil.Completed,
                                                      resourceGroupName,
                                                      new ResourceGroupData(new AzureLocation(resourceGroupLocation)));


            //Create a retry policy that retries on 429 errors. This is because we've been getting a number of flaky test failures
            RetryPolicy = RetryPolicyFactory.CreateForHttp429();

            await ConfigureTestResources(response.Value);
        }

        protected abstract Task ConfigureTestResources(ResourceGroupResource resourceGroup);

        [OneTimeTearDown]
        public async Task Cleanup()
        {
            await ArmClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName))
                     .DeleteAsync(WaitUntil.Started);
        }

        protected async Task AssertContent(string hostName, string actualText, string rootPath = null)
        {
            var result = await client.GetStringAsync($"https://{hostName}/{rootPath}");

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