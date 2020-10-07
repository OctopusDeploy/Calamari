using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Tests.Shared;
using FluentAssertions;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using NUnit.Framework;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;

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
        IResourceGroup resourceGroup;
        private string resourceGroupName;

        readonly HttpClient client = new HttpClient();

        [OneTimeSetUp]
        public async Task Setup()
        {
            clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            resourceGroupName = SdkContext.RandomResourceName(nameof(DeployAzureWebZipCommandFixture), 60);

            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(clientId, clientSecret, tenantId,
                AzureEnvironment.AzureGlobalCloud);

            webappName = "CMOcto";

        }

        [OneTimeTearDown]
        public async Task CleanupCode()
        {

        }

        [Test]
        public async Task Deploy_WebAppZip_Simple()
        {
            var tempPath = TemporaryDirectory.Create();
            new DirectoryInfo(tempPath.DirectoryPath).CreateSubdirectory("AzureZipDeployPackage");
            await File.WriteAllTextAsync(Path.Combine($"{tempPath.DirectoryPath}/AzureZipDeployPackage", "index.html"), "Hello World");
            ZipFile.CreateFromDirectory($"{tempPath.DirectoryPath}/AzureZipDeployPackage", $"{tempPath.DirectoryPath}/AzureZipDeployPackage.1.0.0.zip");

            await CommandTestBuilder.CreateAsync<DeployAzureWebAppZipCommand, Program>().WithArrange(context =>
                {
                    //context.WithFilesToCopy($"{tempPath.DirectoryPath}.zip");
                    context.WithPackage($"{tempPath.DirectoryPath}/AzureZipDeployPackage.1.0.0.zip", "AzureZipDeployPackage", "1.0.0");
                    AddDefaults(context, webappName);
                })
                .Execute();
            await AssertContent($"{webappName}.azurewebsites.net", "Hello World");
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

    }
}