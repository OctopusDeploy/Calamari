using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Terraform.Tests;

[TestFixture]
[Category(TestCategory.ExternalCloudIntegration)]
public class AwsIntegrationTests() : TerraformTests(TerraformCliVersions.V1_15_8)
{
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

        var output = await ExecuteAndReturnResult(PlanCommand, PopulateVariables, temporaryFolder.DirectoryPath);
        output.OutputVariables.ContainsKey("TerraformPlanOutput").Should().BeTrue();

        output = await ExecuteAndReturnResult(ApplyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
        output.OutputVariables.ContainsKey("TerraformValueOutputs[url]").Should().BeTrue();
        output.OutputVariables["TerraformValueOutputs[url]"].Value.Should().Be(expectedUrl);

        string fileData;
        using (var client = new HttpClient())
            fileData = await client.GetStringAsync(expectedUrl).ConfigureAwait(false);

        fileData.Should().Be("Hello World from AWS");

        await ExecuteAndReturnResult(DestroyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
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
            _.Variables.Add("Octopus.Action.Amazon.SecretKey", secretKey);
            _.Variables.Add("Octopus.Action.Aws.Region", "ap-southeast-1");
            _.Variables.Add("Hello", "Hello World from AWS");
            _.Variables.Add("bucket_name", bucketName);
            _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
            _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AWSManagedAccount, "AWS");
            _.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, temporaryFolder.DirectoryPath);
        }
    }
}