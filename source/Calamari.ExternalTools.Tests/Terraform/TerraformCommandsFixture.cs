using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.ExternalTools.Tests.Infrastructure;
using Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies;
using Calamari.Terraform;
using Calamari.Terraform.Commands;
using Calamari.Testing;
using Calamari.Testing.Azure;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Calamari.ExternalTools.Tests.Terraform
{
    [TestFixture]
    public class TerraformCommandsFixture : ExternalToolFixture
    {
        protected override string PrimaryToolName => "terraform";

        protected override Task<string> DownloadTool(string destinationDir, string version, HttpClient client)
            => TerraformStrategy.Download(destinationDir, version, client);

        readonly string planCommand = GetCommandFromType(typeof(PlanCommand));
        readonly string applyCommand = GetCommandFromType(typeof(ApplyCommand));
        readonly string destroyCommand = GetCommandFromType(typeof(DestroyCommand));
        readonly string destroyPlanCommand = GetCommandFromType(typeof(DestroyPlanCommand));

        Version TerraformCliVersionAsObject => new(ToolVersion);

        /// <summary>
        /// Path prefix for test resource directories, relative to the output directory.
        /// Resources live under Terraform/ in the ExternalTools.Tests project.
        /// </summary>
        const string ResourceRoot = "Terraform";

        static string GetTestResourcePath(string relativePath)
            => TestEnvironment.GetTestPath(ResourceRoot, relativePath);

        static string GetTestResourcePath(params string[] paths)
            => TestEnvironment.GetTestPath(new[] { ResourceRoot }.Concat(paths).ToArray());

        static string LoadTextTemplate(string templateName)
            => File.ReadAllText(GetTestResourcePath("CommonTemplates", templateName));

        [OneTimeTearDown]
        public static void OneTimeTearDown()
        {
            ClearTestDirectories();
        }

        static void ClearTestDirectories()
        {
            static void TryDeleteFile(string path)
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
            }

            static void TryDeleteDirectory(string path, bool recursive)
            {
                try
                {
                    Directory.Delete(path, recursive);
                }
                catch (IOException)
                {
                }
            }

            static void ClearTerraformDirectory(string directory)
            {
                var fullPath = GetTestResourcePath(directory);
                TryDeleteFile(Path.Combine(fullPath, "terraform.tfstate"));
                TryDeleteFile(Path.Combine(fullPath, "terraform.tfstate.backup"));
                TryDeleteFile(Path.Combine(fullPath, "terraform.log"));
                TryDeleteDirectory(Path.Combine(fullPath, ".terraform"), true);
                TryDeleteDirectory(Path.Combine(fullPath, "terraform.tfstate.d"), true);
                TryDeleteDirectory(Path.Combine(fullPath, "terraformplugins"), true);
            }

            ClearTerraformDirectory("AWS");
            ClearTerraformDirectory("Azure");
            ClearTerraformDirectory("GoogleCloud");
            ClearTerraformDirectory("PlanDetailedExitCode");
            ClearTerraformDirectory("Simple");
            ClearTerraformDirectory("WithOutputSensitiveVariables");
            ClearTerraformDirectory("WithVariablesSubstitution");
        }

        /// <summary>
        /// Single end-to-end test validating the full Terraform pipeline works with the manifest version.
        /// Runs apply against the Simple directory (no cloud creds needed).
        /// </summary>
        [Test]
        public void ApplySimple_Succeeds()
        {
            ExecuteAndReturnLogOutput(applyCommand, _ => { }, "Simple")
                .Should()
                .NotContain("Error");
        }

        /// <summary>
        /// Validates inline template support works end-to-end with the manifest Terraform version.
        /// </summary>
        [Test]
        public void InlineJsonTemplate_ProducesExpectedOutput()
        {
            string template = LoadTextTemplate("SingleVariable.json");
            var randomNumber = new Random().Next().ToString();

            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add("RandomNumber", randomNumber);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters,
                                                          "{\"ami\":\"test-value\"}");
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Inline);
                                      },
                                      String.Empty,
                                      _ =>
                                      {
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[ami]").Should().BeTrue();
                                          _.OutputVariables["TerraformValueOutputs[ami]"].Value.Should().Be("test-value");
                                      });
        }

        /// <summary>
        /// Validates the full substitution → apply → output pipeline:
        /// Octostache substitution runs on variable files before terraform uses them.
        /// Tests convention ordering in the Calamari pipeline.
        /// </summary>
        [Test]
        public void OutputAndSubstituteOctopusVariables()
        {
            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.txt");
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "example.txt");
                                          _.Variables.Add("Octopus.Action.StepName", "Step Name");
                                          _.Variables.Add("Should_Be_Substituted", "Hello World");
                                          _.Variables.Add("Should_Be_Substituted_in_txt", "Hello World from text");
                                      },
                                      "WithVariablesSubstitution",
                                      result =>
                                      {
                                          result.OutputVariables
                                                .ContainsKey("TerraformValueOutputs[my_output]")
                                                .Should()
                                                .BeTrue();
                                          result.OutputVariables["TerraformValueOutputs[my_output]"]
                                                .Value
                                                .Should()
                                                .Be("Hello World");
                                          result.OutputVariables
                                                .ContainsKey("TerraformValueOutputs[my_output_from_txt_file]")
                                                .Should()
                                                .BeTrue();
                                          result.OutputVariables["TerraformValueOutputs[my_output_from_txt_file]"]
                                                .Value
                                                .Should()
                                                .Be("Hello World from text");
                                      });
        }

        /// <summary>
        /// Validates terraform sensitive outputs are marked as IsSensitive in Calamari output variables.
        /// Tool-specific output parsing behavior.
        /// </summary>
        [Test]
        public void WithOutputSensitiveVariables()
        {
            ExecuteAndReturnLogOutput(applyCommand,
                                      _ => { },
                                      "WithOutputSensitiveVariables",
                                      result =>
                                      {
                                          result.OutputVariables.Values.Should().OnlyContain(variable => variable.IsSensitive);
                                      });
        }

        /// <summary>
        /// Validates plan → apply → plan cycle with state file management.
        /// Exit code 2 means "changes detected", exit code 0 means "no changes".
        /// Unique to terraform's detailed exit code behavior.
        /// </summary>
        [Test]
        public async Task PlanDetailedExitCode()
        {
            using var stateFileFolder = TemporaryDirectory.Create();

            var output = await ExecuteAndReturnResult(planCommand, PopulateVariables, "PlanDetailedExitCode");
            output.OutputVariables.ContainsKey("TerraformPlanDetailedExitCode").Should().BeTrue();
            output.OutputVariables["TerraformPlanDetailedExitCode"].Value.Should().Be("2");

            output = await ExecuteAndReturnResult(applyCommand, PopulateVariables, "PlanDetailedExitCode");
            output.FullLog.Should()
                  .Contain("apply -auto-approve");

            output = await ExecuteAndReturnResult(planCommand, PopulateVariables, "PlanDetailedExitCode");
            output.OutputVariables.ContainsKey("TerraformPlanDetailedExitCode").Should().BeTrue();
            output.OutputVariables["TerraformPlanDetailedExitCode"].Value.Should().Be("0");
            return;

            void PopulateVariables(CommandTestBuilderContext _)
            {
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams,
                                $"-state=\"{Path.Combine(stateFileFolder.DirectoryPath, "terraform.tfstate")}\" -refresh=false");
            }
        }

        [Test]
        public async Task GoogleCloudIntegration()
        {
            var bucketName = $"e2e-tf-{Guid.NewGuid().ToString("N").Substring(0, 6)}";

            using var temporaryFolder = TemporaryDirectory.Create();
            CopyAllFiles(GetTestResourcePath("GoogleCloud"), temporaryFolder.DirectoryPath);

            var environmentJsonKey = await ExternalVariables.Get(ExternalVariable.GoogleCloudJsonKeyfile, CancellationToken.None);
            var jsonKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(environmentJsonKey));

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
            var strategy = TestingRetryPolicies.CreateGoogleCloudHttpRetryPipeline();
            using (var client = new HttpClient())
            {
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
            CopyAllFiles(GetTestResourcePath("Azure"), temporaryFolder.DirectoryPath, ToolVersion);

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
                                                    "Name or service not known",
                                                    "nodename nor servname provided, or not known"
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

        [Test]
        [Ignore("Test needs to be updated because s3 bucket doesn't seem to support ACLs anymore.")]
        public async Task AWSIntegration()
        {
            var bucketName = $"cfe2e-tf-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            var expectedUrl = $"https://{bucketName}.s3.amazonaws.com/test.txt";

            using var temporaryFolder = TemporaryDirectory.Create();
            CopyAllFiles(GetTestResourcePath("AWS"), temporaryFolder.DirectoryPath);

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
                _.Variables.Add("Octopus.Action.Amazon.SecretKey", secretKey);
                _.Variables.Add("Octopus.Action.Aws.Region", "ap-southeast-1");
                _.Variables.Add("Hello", "Hello World from AWS");
                _.Variables.Add("bucket_name", bucketName);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AWSManagedAccount, "AWS");
                _.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, temporaryFolder.DirectoryPath);
            }
        }

        static void CopyAllFiles(string sourceFolderPath, string destinationFolderPath, string? terraformVersion = null)
        {
            if (Directory.Exists(sourceFolderPath))
            {
                if (terraformVersion != null && Directory.Exists(Path.Combine(sourceFolderPath, terraformVersion)))
                {
                    sourceFolderPath = Path.Combine(sourceFolderPath, terraformVersion);
                }

                var filePaths = Directory.GetFiles(sourceFolderPath);

                foreach (var filePath in filePaths)
                {
                    var fileName = Path.GetFileName(filePath);
                    var destFilePath = Path.Combine(destinationFolderPath, fileName);
                    File.Copy(filePath, destFilePath, true);
                }
            }
            else
            {
                throw new Exception($"'{nameof(sourceFolderPath)}' ({sourceFolderPath}) does not exist!");
            }
        }

        string ExecuteAndReturnLogOutput(string command,
                                         Action<CommandTestBuilderContext> populateVariables,
                                         string folderName,
                                         Action<TestCalamariCommandResult>? assert = null)
        {
            return ExecuteAndReturnResult(command, populateVariables, folderName, assert).Result.FullLog;
        }

        async Task<TestCalamariCommandResult> ExecuteAndReturnResult(string command, Action<CommandTestBuilderContext> populateVariables, string folderName, Action<TestCalamariCommandResult>? assert = null)
        {
            var assertResult = assert ?? (_ => { });

            var terraformFiles = Path.IsPathRooted(folderName) ? folderName : GetTestResourcePath(folderName);

            var result = await CommandTestBuilder.CreateAsync<Calamari.Terraform.Program>(command)
                                                 .WithArrange(context =>
                                                              {
                                                                  context.Variables.Add(ScriptVariables.ScriptSource,
                                                                                        ScriptVariables.ScriptSourceOptions.Package);
                                                                  context.Variables.Add(TerraformSpecialVariables.Packages.PackageId, terraformFiles);
                                                                  context.Variables.Add(TerraformSpecialVariables.Calamari.TerraformCliPath,
                                                                                        Path.GetDirectoryName(ToolExecutablePath));
                                                                  context.Variables.Add(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable,
                                                                                        ToolExecutablePath);

                                                                  populateVariables(context);

                                                                  var isInline = context.Variables.Get(ScriptVariables.ScriptSource)!
                                                                                        .Equals(ScriptVariables.ScriptSourceOptions.Inline, StringComparison.InvariantCultureIgnoreCase);
                                                                  if (isInline)
                                                                  {
                                                                      var template = context.Variables.Get(TerraformSpecialVariables.Action.Terraform.Template);
                                                                      var variables = context.Variables.Get(TerraformSpecialVariables.Action.Terraform.TemplateParameters);
                                                                      var isJsonFormat = true;

                                                                      try
                                                                      {
                                                                          JToken.Parse(template);
                                                                      }
                                                                      catch
                                                                      {
                                                                          isJsonFormat = false;
                                                                      }

                                                                      context.WithDataFileNoBom(
                                                                                                template!,
                                                                                                isJsonFormat ? TerraformSpecialVariables.JsonTemplateFile : TerraformSpecialVariables.HclTemplateFile);
                                                                      context.WithDataFileNoBom(
                                                                                                variables!,
                                                                                                isJsonFormat ? TerraformSpecialVariables.JsonVariablesFile : TerraformSpecialVariables.HclVariablesFile);
                                                                  }

                                                                  if (!String.IsNullOrEmpty(folderName))
                                                                  {
                                                                      context.WithFilesToCopy(terraformFiles);
                                                                  }
                                                              })
                                                 .Execute();

            assertResult(result);
            return result;
        }

        static string GetCommandFromType(Type commandType)
        {
            return commandType.CustomAttributes.Where(t => t.AttributeType == typeof(Calamari.Common.Commands.CommandAttribute))
                              .Select(c => c.ConstructorArguments.First().Value)
                              .Single()
                              ?.ToString()!;
        }

        void IgnoreIfVersionIsNotInRange(string minimum, string maximum, string because)
        {
            var minimumVersion = new Version(minimum);
            var maximumVersion = new Version(maximum ?? "999.0.0");

            if (TerraformCliVersionAsObject.CompareTo(minimumVersion) < 0
                || TerraformCliVersionAsObject.CompareTo(maximumVersion) >= 0)
            {
                var becauseText = because is not null ? $" because {because}" : null;
                Assert.Ignore($"Test ignored as terraform version is not between {minimumVersion} and {maximumVersion}{becauseText}");
            }
        }
    }
}
