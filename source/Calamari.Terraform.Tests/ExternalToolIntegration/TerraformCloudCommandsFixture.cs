using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Azure;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Terraform.Tests.ExternalToolIntegration
{
    // These fixtures provision and tear down real resources in GCP/Azure/AWS via the terraform CLI.
    // They need both the terraform binary and cloud credentials; under "tool wins" they are
    // ExternalToolIntegration (inherited from the base), not ExternalCloudIntegration.
    [TestFixture("0.13.7")]
    [TestFixture("1.8.5")]
    public class TerraformCloudCommandsFixture : TerraformCommandsFixtureBase
    {
        public TerraformCloudCommandsFixture(string version) : base(version)
        {
        }

        [Test]
        public async Task GoogleCloudIntegration()
        {
            var bucketName = $"e2e-tf-{Guid.NewGuid().ToString("N").Substring(0, 6)}";

            using var temporaryFolder = TemporaryDirectory.Create();
            CopyAllFiles(TestEnvironment.GetTestPath("GoogleCloud"), temporaryFolder.DirectoryPath);

            var environmentJsonKey = await ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile, CancellationToken.None);
            var jsonKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(environmentJsonKey));

            void PopulateVariables(CommandTestBuilderContext _)
            {
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "test.txt");
                _.Variables.Add("Hello", "Hello World from Google Cloud");
                _.Variables.Add("bucket_name", bucketName);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                _.Variables.Add("Octopus.Action.Terraform.GoogleCloudAccount", bool.TrueString);
                _.Variables.Add("Octopus.Action.GoogleCloudAccount.JsonKey", jsonKey);
                _.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, temporaryFolder.DirectoryPath);
            }

            var output = await ExecuteAndReturnResult(planCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            output.OutputVariables.ContainsKey("TerraformPlanOutput").Should().BeTrue();

            output = await ExecuteAndReturnResult(applyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            output.OutputVariables.ContainsKey("TerraformValueOutputs[url]").Should().BeTrue();
            var requestUri = output.OutputVariables["TerraformValueOutputs[url]"].Value;

            string fileData;
            // This intermittently throws a 401, requiring authorization. These buckets are public by default and the client has no authorization so this looks to be a race condition in the bucket configuration.
            var strategy = TestingRetryPolicies.CreateGoogleCloudHttpRetryPipeline();
            using (var client = new HttpClient())
            {
                //we perform checking in a retry as sometimes it's not quite ready by the time we want to request it
                var response = await strategy.ExecuteAsync(async _ => await client.GetAsync(requestUri));
                response.IsSuccessStatusCode.Should().BeTrue();
                fileData = await response.Content.ReadAsStringAsync();
            }

            fileData.Should().Be("Hello World from Google Cloud");

            await ExecuteAndReturnResult(destroyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            using (var client = new HttpClient())
            {
                var response = await strategy.ExecuteAsync(async _ => await client.GetAsync($"{requestUri}&bust_cache"));
                response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }
        }

        [Test]
        public async Task AzureIntegration()
        {
            var resourceGroupName = AzureTestResourceHelpers.GetResourceGroupName();
            var resourceGroupLocation = RandomAzureRegion.GetRandomRegionWithExclusions();

            var subscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, CancellationToken.None);
            var tenantId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, CancellationToken.None);
            var clientId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, CancellationToken.None);
            var clientPassword = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, CancellationToken.None);

            var random = Guid.NewGuid().ToString("N").Substring(0, 6);
            var appName = $"cfe2e-{random}";
            var expectedHostName = $"{appName}.azurewebsites.net";

            using var temporaryFolder = TemporaryDirectory.Create();
            CopyAllFiles(TestEnvironment.GetTestPath("Azure"), temporaryFolder.DirectoryPath, terraformCliVersion);

            var output = await ExecuteAndReturnResult(planCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            output.OutputVariables.ContainsKey("TerraformPlanOutput").Should().BeTrue();

            output = await ExecuteAndReturnResult(applyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            output.OutputVariables.ContainsKey("TerraformValueOutputs[url]").Should().BeTrue();
            output.OutputVariables["TerraformValueOutputs[url]"].Value.Should().Be(expectedHostName);
            await AssertRequestResponse(HttpStatusCode.Forbidden);

            await ExecuteAndReturnResult(destroyCommand, PopulateVariables, temporaryFolder.DirectoryPath);

            await AssertResponseIsNotReachable();
            return;

            void PopulateVariables(CommandTestBuilderContext _)
            {
                _.Variables.Add(AzureAccountVariables.SubscriptionId,subscriptionId );
                _.Variables.Add(AzureAccountVariables.TenantId,tenantId);
                _.Variables.Add(AzureAccountVariables.ClientId,clientId);
                _.Variables.Add(AzureAccountVariables.Password, clientPassword);
                _.Variables.Add("app_name", appName);
                _.Variables.Add("resource_group_name", resourceGroupName);
                _.Variables.Add("resource_group_location", resourceGroupLocation);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AzureManagedAccount, Boolean.TrueString);
                _.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, temporaryFolder.DirectoryPath);
            }

            async Task AssertResponseIsNotReachable()
            {
                //This will throw on some platforms and return "NotFound" on others
                try
                {
                    await AssertRequestResponse(HttpStatusCode.NotFound);
                }
                catch (HttpRequestException ex)
                {
                    switch (ex.InnerException)
                    {
                        case SocketException socketException:
                            socketException.Message.Should()
                                           .BeOneOf(
                                                    "No such host is known.",
                                                    "Name or service not known", //Some Linux distros
                                                    "nodename nor servname provided, or not known" //Mac
                                                   );
                            break;
                        case WebException webException:
                            webException.Message.Should()
                                        .StartWith("The remote name could not be resolved");
                            break;
                        default:
                            throw;
                    }
                }
            }

            async Task AssertRequestResponse(HttpStatusCode expectedStatusCode)
            {
                using var client = new HttpClient();
                var response = await client.GetAsync($"https://{expectedHostName}").ConfigureAwait(false);
                response.StatusCode.Should().Be(expectedStatusCode);
            }
        }

        //TODO: #team-modern-deployments-requests-and-discussion
        [Test]
        [Ignore("Test needs to be updated because s3 bucket doesn't seem to support ACLs anymore.")]
        public async Task AWSIntegration()
        {
            var bucketName = $"cfe2e-tf-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            var expectedUrl = $"https://{bucketName}.s3.amazonaws.com/test.txt";

            using var temporaryFolder = TemporaryDirectory.Create();
            CopyAllFiles(TestEnvironment.GetTestPath("AWS"), temporaryFolder.DirectoryPath);

            var accessKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, CancellationToken.None);
            var secretKey = await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, CancellationToken.None);

            var output = await ExecuteAndReturnResult(planCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            output.OutputVariables.ContainsKey("TerraformPlanOutput").Should().BeTrue();

            output = await ExecuteAndReturnResult(applyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            output.OutputVariables.ContainsKey("TerraformValueOutputs[url]").Should().BeTrue();
            output.OutputVariables["TerraformValueOutputs[url]"].Value.Should().Be(expectedUrl);

            string fileData;
            using (var client = new HttpClient())
                fileData = await client.GetStringAsync(expectedUrl).ConfigureAwait(false);

            fileData.Should().Be("Hello World from AWS");

            await ExecuteAndReturnResult(destroyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(expectedUrl).ConfigureAwait(false);
                response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }

            return;

            void PopulateVariables(CommandTestBuilderContext _)
            {
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "test.txt");
                _.Variables.Add("Octopus.Action.Amazon.AccessKey", accessKey);
                _.Variables.Add("Octopus.Action.Amazon.SecretKey",secretKey);
                _.Variables.Add("Octopus.Action.Aws.Region", "ap-southeast-1");
                _.Variables.Add("Hello", "Hello World from AWS");
                _.Variables.Add("bucket_name", bucketName);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AWSManagedAccount, "AWS");
                _.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, temporaryFolder.DirectoryPath);
            }
        }
    }
}
