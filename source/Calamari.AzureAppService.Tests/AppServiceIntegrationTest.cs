﻿using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Calamari.Azure;
using Calamari.Tests.Shared;
using FluentAssertions;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using NUnit.Framework;

namespace Calamari.AzureAppService.Tests
{
    public abstract class AppServiceIntegrationTest
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
        
        [OneTimeSetUp]
        public async Task Setup()
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ??
                DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ??
                DefaultVariables.ActiveDirectoryEndpoint;

            resourceGroupName = Guid.NewGuid().ToString();

            clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            resourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? "eastus";

            authToken = await Auth.GetAuthTokenAsync(activeDirectoryEndpointBaseUri, resourceManagementEndpointBaseUri,
                tenantId, clientId, clientSecret);

            var resourcesClient = new ResourcesManagementClient(subscriptionId,
                new ClientSecretCredential(tenantId, clientId, clientSecret));

            resourceGroupClient = resourcesClient.ResourceGroups;

            var resourceGroup = new ResourceGroup(resourceGroupLocation);
            resourceGroup = await resourceGroupClient.CreateOrUpdateAsync(resourceGroupName, resourceGroup);
            
            webMgmtClient = new WebSiteManagementClient(new TokenCredentials(authToken))
            {
                SubscriptionId = subscriptionId,
                HttpClient = { BaseAddress = new Uri(DefaultVariables.ResourceManagementEndpoint) },
            };

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
