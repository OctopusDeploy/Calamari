using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Calamari.Terraform.Tests;

[TestFixture]
[Category(TestCategory.ExternalCloudIntegration)]
public class GoogleCloudIntegrationTests() : TerraformTests(TerraformCliVersions.V1_15_8)
{
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

        var output = await ExecuteAndReturnResult(PlanCommand, PopulateVariables, temporaryFolder.DirectoryPath);
        output.OutputVariables.ContainsKey("TerraformPlanOutput").Should().BeTrue();

        output = await ExecuteAndReturnResult(ApplyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
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

        await ExecuteAndReturnResult(DestroyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
        using (var client = new HttpClient())
        {
            var response = await strategy.ExecuteAsync(async _ => await client.GetAsync($"{requestUri}&bust_cache"));
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    [Test]
    public async Task GoogleCloudIntegrationWithGcsBackend()
    {
        var bucketName = Guid.NewGuid().ToString("N");
        var jsonKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(await ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile, CancellationToken.None)));

        void AddGoogleCloudAccount(CommandTestBuilderContext _)
        {
            _.Variables.Add("Octopus.Action.Terraform.GoogleCloudAccount", bool.TrueString);
            _.Variables.Add("Octopus.Action.GoogleCloudAccount.JsonKey", jsonKey);
        }

        using var bucketFolder = TemporaryDirectory.Create();
        CopyAllFiles(TestEnvironment.GetTestPath("GoogleCloudBackend"), bucketFolder.DirectoryPath);

        void PopulateBucketVariables(CommandTestBuilderContext _)
        {
            AddGoogleCloudAccount(_);
            _.Variables.Add("bucket_name", bucketName);
            _.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, bucketFolder.DirectoryPath);
        }

        try
        {
            await ExecuteAndReturnResult(ApplyCommand, PopulateBucketVariables, bucketFolder.DirectoryPath);

            // Fails at terraform init unless the account's credentials reach the gcs backend
            await ExecuteAndReturnResult(ApplyCommand,
                _ =>
                {
                    AddGoogleCloudAccount(_);
                    _.Variables.Add(ScriptVariables.ScriptSource, ScriptVariables.ScriptSourceOptions.Inline);
                    _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, "");
                    _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template,
                        $@"terraform {{
  backend ""gcs"" {{
    bucket = ""{bucketName}""
  }}
}}");
                },
                String.Empty);
        }
        finally
        {
            await ExecuteAndReturnResult(DestroyCommand, PopulateBucketVariables, bucketFolder.DirectoryPath);
        }
    }

    [TestCase(null, "Set GOOGLE_BACKEND_CREDENTIALS with JSON key for test@example.com")]
    [TestCase("{}", "Did not set GOOGLE_BACKEND_CREDENTIALS because GOOGLE_CREDENTIALS is set")]
    public void GoogleCredentialsMappingTakesPriorityForGcsBackend(string? mappedGoogleCredentials, string expectedLog)
    {
        var log = ExecuteAndReturnLogOutput(ApplyCommand,
            _ =>
            {
                _.Variables.Add("Octopus.Action.Terraform.GoogleCloudAccount", bool.TrueString);
                _.Variables.Add("Octopus.Action.GoogleCloudAccount.JsonKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(@"{""client_email"": ""test@example.com""}")));
                if (mappedGoogleCredentials != null)
                    _.Variables.Add(TerraformSpecialVariables.Action.Terraform.EnvironmentVariables,
                        JsonConvert.SerializeObject(new Dictionary<string, string> { { "GOOGLE_CREDENTIALS", mappedGoogleCredentials } }));
                _.Variables.Add(ScriptVariables.ScriptSource, ScriptVariables.ScriptSourceOptions.Inline);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, "");
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, "");
            },
            String.Empty);

        log.Should().Contain(expectedLog);
    }
}