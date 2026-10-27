using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
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

namespace Calamari.Terraform.Tests;

[TestFixture]
[Category(TestCategory.ExternalCloudIntegration)]
public class AzureIntegrationTests() : TerraformTests(TerraformCliVersions.V1_15_8)
{
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
        CopyAllFiles(TestEnvironment.GetTestPath("Azure"), temporaryFolder.DirectoryPath, CliVersion);

        var output = await ExecuteAndReturnResult(PlanCommand, PopulateVariables, temporaryFolder.DirectoryPath);
        output.OutputVariables.ContainsKey("TerraformPlanOutput").Should().BeTrue();

        output = await ExecuteAndReturnResult(ApplyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
        output.OutputVariables.ContainsKey("TerraformValueOutputs[url]").Should().BeTrue();
        output.OutputVariables["TerraformValueOutputs[url]"].Value.Should().Be(expectedHostName);
        await AssertRequestResponse(HttpStatusCode.Forbidden);

        await ExecuteAndReturnResult(DestroyCommand, PopulateVariables, temporaryFolder.DirectoryPath);

        await AssertResponseIsNotReachable();
        return;

        void PopulateVariables(CommandTestBuilderContext _)
        {
            _.Variables.Add(AzureAccountVariables.SubscriptionId, subscriptionId);
            _.Variables.Add(AzureAccountVariables.TenantId, tenantId);
            _.Variables.Add(AzureAccountVariables.ClientId, clientId);
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
}