using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Tests.Shared;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AzureWebAppZip.Tests
{
    [TestFixture]
    public class DeployAzureWebZipCommandFixture
    {
        private string clientId;
        private string clientSecret;
        private string webappName;

        readonly HttpClient client = new HttpClient();

        [SetUp]
        public async Task Setup()
        {
            clientId = "27312afb-009f-4fed-a8bb-9737425cc42a"; //ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            clientSecret = "EU.M~6P3pCHe4K__x3~jif.keOtae5A7Xz"; //ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            webappName = "CMOcto";
        }

        [OneTimeTearDown]
        public async Task CleanupCode()
        {

        }
        [Test]
        public async Task Deploy_WebAppZip_Simple()
        {
            using var tempPath = TemporaryDirectory.Create();
            await File.WriteAllTextAsync(Path.Combine(tempPath.DirectoryPath, "index.html"), "Hello World");
            ZipFile.CreateFromDirectory(tempPath.DirectoryPath, $"{tempPath.DirectoryPath}.zip");

            await CommandTestBuilder.CreateAsync<DeployAzureWebAppZipCommand, Program>().WithArrange(context =>
                {
                    AddDefaults(context, webappName);
                    context.WithFilesToCopy($"{tempPath.DirectoryPath}.zip");
                })
                .Execute();
            await AssertContent($"{webappName}.azurewebsites.net", "Hello World");
        }

        void AddDefaults(CommandTestBuilderContext context, string webAppName)
        {
            context.Variables.Add(AzureAccountVariables.ClientId, clientId);
            context.Variables.Add(AzureAccountVariables.Password, clientSecret);
        }

        async Task AssertContent(string hostName, string actualText, string rootPath = null)
        {
            var result = await client.GetStringAsync($"https://{hostName}/{rootPath}");

            result.Should().Be(actualText);
        }

    }
}