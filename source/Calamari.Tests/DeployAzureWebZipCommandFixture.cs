using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Calamari.Azure;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Tests.Shared;
using FluentAssertions;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NUnit.Framework;

namespace Calamari.AzureWebAppZip.Tests
{
    [TestFixture]
    public class DeployAzureWebZipCommandFixture
    {
        private string clientId;
        private string clientSecret;
        private string tenantId;
        private string subscriptionId;
        private string webappName;
        private string resourceGroupName;
        private ResourceGroupsOperations resourceGroupClient;
        private Application webapp;

        readonly HttpClient client = new HttpClient();

        [OneTimeSetUp]
        public async Task Setup()
        {
            resourceGroupName = Guid.NewGuid().ToString();

            clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            //var token = GetAuthToken(tenantId, clientId, clientSecret);
            
            //var resourcesClient = new ResourcesManagementClient(subscriptionId,
            //    new ClientSecretCredential(tenantId, clientId, clientSecret));

            var resourcesClient = new ResourcesManagementClient(subscriptionId, new AzureCliCredential());

            resourceGroupClient = resourcesClient.ResourceGroups;
            var resourceWebappClient = resourcesClient.Applications;

            var resourceGroup = new ResourceGroup("eastus"); 
            resourceGroup = await resourceGroupClient.CreateOrUpdateAsync(resourceGroupName, resourceGroup);

            webapp = new Application("MarketPlace", resourceGroup.Id);
            var createOperation =
                await resourceWebappClient.StartCreateOrUpdateAsync(resourceGroupName, resourceGroupName, webapp);

            while (!createOperation.HasCompleted)
            {
                await Task.Delay(500);
            }
        }

        [OneTimeTearDown]
        public async Task CleanupCode()
        {
            var opp = await resourceGroupClient.StartDeleteAsync(resourceGroupName);

            while (!opp.HasCompleted)
            {
                await Task.Delay(500);
            }
        }

        //[Test]
        public async Task Deploy_WebAppZip_Simple()
        {
            await Task.Delay(500);
            //var tempPath = TemporaryDirectory.Create();
            //new DirectoryInfo(tempPath.DirectoryPath).CreateSubdirectory("AzureZipDeployPackage");
            ////await File.WriteAllTextAsync(Path.Combine($"{tempPath.DirectoryPath}/AzureZipDeployPackage", "index.html"), "Hello World");
            //ZipFile.CreateFromDirectory($"{tempPath.DirectoryPath}/AzureZipDeployPackage", $"{tempPath.DirectoryPath}/AzureZipDeployPackage.1.0.0.zip");

            //await CommandTestBuilder.CreateAsync<DeployAzureWebAppZipCommand, Program>().WithArrange(context =>
            //    {
            //        //context.WithFilesToCopy($"{tempPath.DirectoryPath}.zip");
            //        context.WithPackage($"{tempPath.DirectoryPath}/AzureZipDeployPackage.1.0.0.zip", "AzureZipDeployPackage", "1.0.0");
            //        AddDefaults(context, webappName);
            //    })
            //    .Execute();
            //await AssertContent($"{webappName}.azurewebsites.net", "Hello World");
        }

        void AddDefaults(CommandTestBuilderContext context, string webAppName)
        {
            context.Variables.Add(AccountVariables.ClientId, clientId);
            context.Variables.Add(AccountVariables.Password, clientSecret);
            context.Variables.Add(AccountVariables.TenantId, tenantId);
            context.Variables.Add(AccountVariables.SubscriptionId, subscriptionId);
            context.Variables.Add("Octopus.Action.Azure.ResourceGroupName", resourceGroupName);
            context.Variables.Add("Octopus.Action.Azure.WebAppName", webAppName);
        }

        async Task AssertContent(string hostName, string actualText, string rootPath = null)
        {
            var result = await client.GetStringAsync($"https://{hostName}/{rootPath}");

            result.Should().Be(actualText);
        }

        private async Task<string> GetAuthToken(string tenantId, string applicationId, string password)
        {
            var activeDirectoryEndPoint = @"https://login.windows.net/";
            var managementEndPoint = @"https://management.azure.com/";
            var authContext = GetContextUri(activeDirectoryEndPoint, tenantId); 
            //Log.Verbose($"Authentication Context: {authContext}");
            var context = new AuthenticationContext(authContext);
            var result = await context.AcquireTokenAsync(managementEndPoint,
                new ClientCredential(applicationId, password));
            
            return result.AccessToken;
        }

        string GetContextUri(string activeDirectoryEndPoint, string tenantId)
        {
            if (!activeDirectoryEndPoint.EndsWith("/"))
            {
                return $"{activeDirectoryEndPoint}/{tenantId}";
            }

            return $"{activeDirectoryEndPoint}{tenantId}";
        }
    }
}