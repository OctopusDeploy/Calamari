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

            ClearTerraformDirectory("AdditionalParams");
            ClearTerraformDirectory("AWS");
            ClearTerraformDirectory("Azure");
            ClearTerraformDirectory("GoogleCloud");
            ClearTerraformDirectory("PlanDetailedExitCode");
            ClearTerraformDirectory("Simple");
            ClearTerraformDirectory($"TemplateDirectory{Path.DirectorySeparatorChar}SubFolder");
            ClearTerraformDirectory("TemplateDirectory");
            ClearTerraformDirectory("WithOutputSensitiveVariables");
            ClearTerraformDirectory("WithVariables");
            ClearTerraformDirectory("WithVariablesSubstitution");
        }

        [Test]
        public void OverridingCacheFolder_WithNonsense_ThrowsAnError()
        {
            ExecuteAndReturnLogOutput("apply-terraform",
                                      _ =>
                                      {
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Package);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.EnvironmentVariables,
                                                          JsonConvert.SerializeObject(new Dictionary<string, string> { { "TF_PLUGIN_CACHE_DIR", "Nonsense" } }));
                                      },
                                      "Simple")
                .Should()
                .ContainAll("The specified plugin cache dir", "cannot be opened");
        }

        [Test]
        public void NotProvidingEnvVariables_DoesNotCrashEverything()
        {
            ExecuteAndReturnLogOutput("apply-terraform",
                                      _ =>
                                      {
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Package);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.EnvironmentVariables, null);
                                      },
                                      "Simple")
                .Should()
                .NotContain("Error");
        }

        [Test]
        public void UserDefinedEnvVariables_OverrideDefaultBehaviour()
        {
            string template = LoadTextTemplate("SingleVariable.json");

            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, "{}");
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Inline);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.EnvironmentVariables,
                                                          JsonConvert.SerializeObject(new Dictionary<string, string> { { "TF_VAR_ami", "new ami value" } }));
                                      },
                                      String.Empty,
                                      _ =>
                                      {
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[ami]").Should().BeTrue();
                                          _.OutputVariables["TerraformValueOutputs[ami]"].Value.Should().Be("new ami value");
                                      });
        }

        [Test]
        public void ExtraInitParametersAreSet()
        {
            IgnoreIfVersionIsNotInRange("0.0.0", "1.0.0", "-get-plugins was removed in 0.15.0/1.0.0");
            var additionalParams = "-var-file=\"backend.tfvars\"";
            ExecuteAndReturnLogOutput(planCommand,
                                      _ =>
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AdditionalInitParams, additionalParams),
                                      "Simple")
                .Should()
                .Contain($"init -get-plugins=true {additionalParams}");
        }

        [Test]
        public void AllowPluginDownloadsShouldBeDisabled()
        {
            IgnoreIfVersionIsNotInRange("0.0.0", "0.15.0", "-get-plugins was removed in 0.15.0/1.0.0");
            ExecuteAndReturnLogOutput(planCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads,
                                                          false.ToString());
                                      },
                                      "Simple")
                .Should()
                .Contain("init -get-plugins=false");
        }

        [Test]
        public void AttachLogFile()
        {
            ExecuteAndReturnLogOutput(planCommand,
                                      _ =>
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AttachLogFile, true.ToString()),
                                      "Simple",
                                      result =>
                                      {
                                          result.Artifacts.Count.Should().Be(1);
                                      });
        }

        [Test]
        [TestCase(typeof(PlanCommand), "plan -detailed-exitcode -var my_var=\"Hello world\"")]
        [TestCase(typeof(ApplyCommand), "apply -auto-approve -var my_var=\"Hello world\"")]
        [TestCase(typeof(DestroyPlanCommand), "plan -detailed-exitcode -destroy -var my_var=\"Hello world\"")]
        [TestCase(typeof(DestroyCommand), "destroy -auto-approve -var my_var=\"Hello world\"")]
        public void AdditionalActionParams(Type commandType, string expected)
        {
            var command = GetCommandFromType(commandType);

            ExecuteAndReturnLogOutput(command,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams, "-var my_var=\"Hello world\"");
                                      },
                                      "AdditionalParams")
                .Should()
                .Contain(expected);
        }

        [Test]
        [TestCase(typeof(PlanCommand), "plan -detailed-exitcode -var-file=\"example.tfvars\"")]
        [TestCase(typeof(ApplyCommand), "apply -auto-approve -var-file=\"example.tfvars\"")]
        [TestCase(typeof(DestroyPlanCommand), "plan -detailed-exitcode -destroy -var-file=\"example.tfvars\"")]
        [TestCase(typeof(DestroyCommand), "destroy -auto-approve -var-file=\"example.tfvars\"")]
        public void VarFiles(Type commandType, string actual)
        {
            ExecuteAndReturnLogOutput(GetCommandFromType(commandType), _ => { _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars"); }, "WithVariables")
                .Should()
                .Contain(actual);
        }

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

        [Test]
        public void EnableNoMatchWarningIsNotSet()
        {
            ExecuteAndReturnLogOutput(applyCommand, _ => { }, "Simple")
                .Should()
                .NotContain("No files were found that match the substitution target pattern");
        }

        [Test]
        public void EnableNoMatchWarningIsNotSetWithAdditionSubstitution()
        {
            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "doesNotExist.txt");
                                      },
                                      "Simple")
                .Should()
                .MatchRegex("No files were found in (.*) that match the substitution target pattern '\\*\\*/\\*\\.tfvars\\.json'")
                .And
                .MatchRegex("No files were found in (.*) that match the substitution target pattern 'doesNotExist.txt'");
        }

        [Test]
        public void EnableNoMatchWarningIsTrue()
        {
            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "doesNotExist.txt");
                                          _.Variables.Add("Octopus.Action.SubstituteInFiles.EnableNoMatchWarning", "true");
                                      },
                                      "Simple")
                .Should()
                .MatchRegex("No files were found in (.*) that match the substitution target pattern '\\*\\*/\\*\\.tfvars\\.json'")
                .And
                .MatchRegex("No files were found in (.*) that match the substitution target pattern 'doesNotExist.txt'");
        }

        [Test]
        public void EnableNoMatchWarningIsFalse()
        {
            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "doesNotExist.txt");
                                          _.Variables.Add("Octopus.Action.SubstituteInFiles.EnableNoMatchWarning", "False");
                                      },
                                      "Simple")
                .Should()
                .NotContain("No files were found that match the substitution target pattern");
        }

        [Test]
        [TestCase(typeof(PlanCommand))]
        [TestCase(typeof(DestroyPlanCommand))]
        public void TerraformPlanOutput(Type commandType)
        {
            ExecuteAndReturnLogOutput(GetCommandFromType(commandType),
                                      _ => { _.Variables.Add("Octopus.Action.StepName", "Step Name"); },
                                      "Simple",
                                      result =>
                                      {
                                          result.OutputVariables
                                                .ContainsKey("TerraformPlanOutput")
                                                .Should()
                                                .BeTrue();
                                      });
        }

        [Test]
        public void UsesWorkSpace()
        {
            ExecuteAndReturnLogOutput(applyCommand, _ => { _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Workspace, "myspace"); }, "Simple")
                .Should()
                .Contain("workspace new \"myspace\"");
        }

        [Test]
        public void UsesTemplateDirectory()
        {
            ExecuteAndReturnLogOutput(applyCommand, _ => { _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateDirectory, "SubFolder"); }, "TemplateDirectory")
                .Should()
                .Contain($"SubFolder{Path.DirectorySeparatorChar}example.tf");
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
        public void InlineHclTemplateAndVariables()
        {
            const string variables = "stringvar = \"default string\"";
            string template = LoadTextTemplate("HclWithVariables.hcl");

            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add("RandomNumber", new Random().Next().ToString());
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, variables);
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Inline);
                                      },
                                      String.Empty,
                                      _ =>
                                      {
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[nestedlist]").Should().BeTrue();
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[nestedmap]").Should().BeTrue();
                                      });
        }

        [Test]
        public void InlineHclTemplateWithMultilineOutput()
        {
            const string expected = @"apiVersion: v1
kind: ConfigMap
metadata:
  name: aws-auth
  namespace: kube-system
data:
  mapRoles: |
    - rolearn: arbitrary text
      username: system:node:username
      groups:
        - system:bootstrappers
        - system:nodes";
            string template = $@"locals {{
  config-map-aws-auth = <<CONFIGMAPAWSAUTH
{expected}
CONFIGMAPAWSAUTH
}}

output ""config-map-aws-auth"" {{
    value = ""${{local.config-map-aws-auth}}""
}}";

            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, "");
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Inline);
                                      },
                                      String.Empty,
                                      _ =>
                                      {
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[config-map-aws-auth]").Should().BeTrue();
                                          _.OutputVariables["TerraformValueOutputs[config-map-aws-auth]"]
                                           .Value?.TrimEnd()
                                           .Replace("\r\n", "\n")
                                           .Should()
                                           .Be($"{expected.Replace("\r\n", "\n")}");
                                      });
        }

        [Test]
        public void CanDetermineTerraformVersion()
        {
            ExecuteAndReturnLogOutput(applyCommand, _ => { _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Workspace, "testversionspace"); }, "Simple")
                .Should()
                .NotContain("Could not parse Terraform CLI version");
        }

        [Test]
        public void InlineJsonTemplateAndVariables()
        {
            const string variables =
                "{\"ami\":\"new ami value\"}";
            string template = LoadTextTemplate("InlineJsonWithVariables.json");

            var randomNumber = new Random().Next().ToString();

            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add("RandomNumber", randomNumber);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, variables);
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Inline);
                                      },
                                      String.Empty,
                                      _ =>
                                      {
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[ami]").Should().BeTrue();
                                          _.OutputVariables["TerraformValueOutputs[ami]"].Value.Should().Be("new ami value");
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[random]").Should().BeTrue();
                                          _.OutputVariables["TerraformValueOutputs[random]"].Value.Should().Be(randomNumber);
                                      });
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
