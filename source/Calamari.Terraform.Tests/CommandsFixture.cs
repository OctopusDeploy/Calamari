using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Retry;
using Calamari.Common.Plumbing.Variables;
using Calamari.Terraform.Commands;
using Calamari.Terraform.Tests.CommonTemplates;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Calamari.Terraform.Tests
{
    [TestFixture("0.11.15")]
    [TestFixture("0.13.0")]
    [TestFixture("1.0.0")]
    public class CommandsFixture
    {
        string? customTerraformExecutable;
        string terraformCliVersion;
        readonly string planCommand = GetCommandFromType(typeof(PlanCommand));
        readonly string applyCommand = GetCommandFromType(typeof(ApplyCommand));
        readonly string destroyCommand = GetCommandFromType(typeof(DestroyCommand));
        readonly string destroyPlanCommand = GetCommandFromType(typeof(DestroyPlanCommand));

        Version TerraformCliVersionAsObject => new(terraformCliVersion);

        public CommandsFixture(string version)
        {
            terraformCliVersion = version;
            InstallTools().GetAwaiter().GetResult();
        }

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
                    File.Delete(TestEnvironment.GetTestPath(path));
                }
                catch (IOException)
                {
                }
            }

            static void TryDeleteDirectory(string path, bool recursive)
            {
                try
                {
                    Directory.Delete(TestEnvironment.GetTestPath(path), recursive);
                }
                catch (IOException)
                {
                }
            }

            static void ClearTerraformDirectory(string directory)
            {
                TryDeleteFile(Path.Combine(directory, "terraform.tfstate"));
                TryDeleteFile(Path.Combine(directory, "terraform.tfstate.backup"));
                TryDeleteFile(Path.Combine(directory, "terraform.log"));
                TryDeleteDirectory(Path.Combine(directory, ".terraform"), true);
                TryDeleteDirectory(Path.Combine(directory, "terraform.tfstate.d"), true);
                TryDeleteDirectory(Path.Combine(directory, "terraformplugins"), true);
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

        public async Task InstallTools()
        {
            ClearTestDirectories(); // pre-emptively clear test directories for better dev experience

            static string GetTerraformFileName(string currentVersion)
            {
                if (CalamariEnvironment.IsRunningOnNix)
                    return $"terraform_{currentVersion}_linux_amd64.zip";
                if (CalamariEnvironment.IsRunningOnMac)
                    return $"terraform_{currentVersion}_darwin_amd64.zip";

                return $"terraform_{currentVersion}_windows_amd64.zip";
            }

            static async Task DownloadTerraform(string fileName,
                                                HttpClient client,
                                                string downloadBaseUrl,
                                                string destination)
            {
                var zipPath = Path.Combine(Path.GetTempPath(), fileName);
                using (new TemporaryFile(zipPath))
                {
                    using (var fileStream =
                           new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var stream = await client.GetStreamAsync($"{downloadBaseUrl}{fileName}"))
                    {
                        await stream.CopyToAsync(fileStream);
                    }

                    ZipFile.ExtractToDirectory(zipPath, destination);
                }
            }

            async Task DownloadCli(string destination, string version)
            {
                Console.WriteLine("Downloading terraform cli...");

                var retry = new RetryTracker(3, TimeSpan.MaxValue, new LimitedExponentialRetryInterval(1000, 30000, 2));
                while (retry.Try())
                {
                    try
                    {
                        using (var client = new HttpClient())
                        {
                            var downloadBaseUrl = $"https://releases.hashicorp.com/terraform/{version}/";
                            var fileName = GetTerraformFileName(version);

                            await DownloadTerraform(fileName, client, downloadBaseUrl, destination);
                        }

                        customTerraformExecutable = Directory.EnumerateFiles(destination)
                                                             .FirstOrDefault(f => Path.GetFileName(f).Contains("terraform"));
                        Console.WriteLine($"Downloaded terraform to {customTerraformExecutable}");

                        AddExecutePermission(customTerraformExecutable!);
                        break;
                    }
                    catch
                    {
                        if (!retry.CanRetry())
                        {
                            throw;
                        }

                        await Task.Delay(retry.Sleep());
                    }
                }
            }

            var destinationDirectoryName = Path.Combine(TestEnvironment.GetTestPath("TerraformCLIPath"), terraformCliVersion);

            if (Directory.Exists(destinationDirectoryName))
            {
                var path = Directory.EnumerateFiles(destinationDirectoryName).FirstOrDefault();
                if (path != null)
                {
                    customTerraformExecutable = path;
                    Console.WriteLine($"Using existing terraform located in {customTerraformExecutable}");
                    return;
                }
            }

            await DownloadCli(destinationDirectoryName, terraformCliVersion);
        }

        [Test]
        public void OverridingCacheFolder_WithNonSense_ThrowsAnError()
        {
            IgnoreIfVersionIsNotInRange("0.15.0");

            ExecuteAndReturnLogOutput("apply-terraform",
                                      _ =>
                                      {
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Package);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.EnvironmentVariables,
                                                          JsonConvert.SerializeObject(new Dictionary<string, string> { { "TF_PLUGIN_CACHE_DIR", "NonSense" } }));
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
            string template = TemplateLoader.LoadTextTemplate("SingleVariable.json");

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
            IgnoreIfVersionIsNotInRange("0.11.15", "0.15.0");
            var additionalParams = "-var-file=\"backend.tfvars\"";
            ExecuteAndReturnLogOutput(planCommand,
                                      _ =>
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AdditionalInitParams, additionalParams),
                                      "Simple")
                .Should()
                .Contain($"init -no-color -get-plugins=true {additionalParams}");
        }

        [Test]
        public void AllowPluginDownloadsShouldBeDisabled()
        {
            IgnoreIfVersionIsNotInRange("0.11.15", "0.15.0");
            ExecuteAndReturnLogOutput(planCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads,
                                                          false.ToString());
                                      },
                                      "Simple")
                .Should()
                .Contain("init -no-color -get-plugins=false");
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
        [TestCase(typeof(PlanCommand), "plan -no-color -detailed-exitcode -var my_var=\"Hello world\"")]
        [TestCase(typeof(ApplyCommand), "apply -no-color -auto-approve -var my_var=\"Hello world\"")]
        [TestCase(typeof(DestroyPlanCommand), "plan -no-color -detailed-exitcode -destroy -var my_var=\"Hello world\"")]
        [TestCase(typeof(DestroyCommand), "destroy -auto-approve -no-color -var my_var=\"Hello world\"")]
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
        [TestCase(typeof(PlanCommand), "plan -no-color -detailed-exitcode -var-file=\"example.tfvars\"")]
        [TestCase(typeof(ApplyCommand), "apply -no-color -auto-approve -var-file=\"example.tfvars\"")]
        [TestCase(typeof(DestroyPlanCommand), "plan -no-color -detailed-exitcode -destroy -var-file=\"example.tfvars\"")]
        [TestCase(typeof(DestroyCommand), "destroy -auto-approve -no-color -var-file=\"example.tfvars\"")]
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
            IgnoreIfVersionIsNotInRange("0.15.0");

            const string jsonEnvironmentVariableKey = "GOOGLECLOUD_OCTOPUSAPITESTER_JSONKEY";

            var bucketName = $"e2e-tf-{Guid.NewGuid().ToString("N").Substring(0, 6)}";

            using var temporaryFolder = TemporaryDirectory.Create();
            CopyAllFiles(TestEnvironment.GetTestPath("GoogleCloud"), temporaryFolder.DirectoryPath);

            var environmentJsonKey = Environment.GetEnvironmentVariable(jsonEnvironmentVariableKey);
            if (environmentJsonKey == null)
            {
                throw new Exception($"Environment Variable `{jsonEnvironmentVariableKey}` could not be found. The value can be found in the password store under GoogleCloud - OctopusAPITester");
            }

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

            // This intermittently throws a 401, requiring authorization. These buckets are public by default and the client has no authorization so this looks to be a race case in the bucket configuration.
            await Task.Delay(TimeSpan.FromSeconds(5));
            using (var client = new HttpClient())
            {
                fileData = await client.GetStringAsync(requestUri).ConfigureAwait(false);
            }

            fileData.Should().Be("Hello World from Google Cloud");

            await ExecuteAndReturnResult(destroyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            await Task.Delay(TimeSpan.FromSeconds(10));
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync($"{requestUri}&bust_cache").ConfigureAwait(false);
                response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }
        }

        [Test]
        public async Task AzureIntegration()
        {
            var random = Guid.NewGuid().ToString("N").Substring(0, 6);
            var appName = $"cfe2e-{random}";
            var expectedHostName = $"{appName}.azurewebsites.net";

            using var temporaryFolder = TemporaryDirectory.Create();
            CopyAllFiles(TestEnvironment.GetTestPath("Azure"), temporaryFolder.DirectoryPath);

            async Task PopulateVariables(CommandTestBuilderContext _)
            {
                _.Variables.Add(AzureAccountVariables.SubscriptionId, await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, CancellationToken.None));
                _.Variables.Add(AzureAccountVariables.TenantId, await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, CancellationToken.None));
                _.Variables.Add(AzureAccountVariables.ClientId, await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, CancellationToken.None));
                _.Variables.Add(AzureAccountVariables.Password, await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, CancellationToken.None));
                _.Variables.Add("app_name", appName);
                _.Variables.Add("random", random);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AzureManagedAccount, Boolean.TrueString);
                _.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, temporaryFolder.DirectoryPath);
            }

            var output = await ExecuteAndReturnResult(planCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            output.OutputVariables.ContainsKey("TerraformPlanOutput").Should().BeTrue();

            output = await ExecuteAndReturnResult(applyCommand, PopulateVariables, temporaryFolder.DirectoryPath);
            output.OutputVariables.ContainsKey("TerraformValueOutputs[url]").Should().BeTrue();
            output.OutputVariables["TerraformValueOutputs[url]"].Value.Should().Be(expectedHostName);
            await AssertRequestResponse(HttpStatusCode.Forbidden);

            await ExecuteAndReturnResult(destroyCommand, PopulateVariables, temporaryFolder.DirectoryPath);

            await AssertResponseIsNotReachable();

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

            async Task PopulateVariables(CommandTestBuilderContext _)
            {
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "test.txt");
                _.Variables.Add("Octopus.Action.Amazon.AccessKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, CancellationToken.None));
                _.Variables.Add("Octopus.Action.Amazon.SecretKey", await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, CancellationToken.None));
                _.Variables.Add("Octopus.Action.Aws.Region", "ap-southeast-1");
                _.Variables.Add("Hello", "Hello World from AWS");
                _.Variables.Add("bucket_name", bucketName);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AWSManagedAccount, "AWS");
                _.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, temporaryFolder.DirectoryPath);
            }

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
        }

        [Test]
        public async Task PlanDetailedExitCode()
        {
            using var stateFileFolder = TemporaryDirectory.Create();

            void PopulateVariables(CommandTestBuilderContext _)
            {
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams,
                                $"-state=\"{Path.Combine(stateFileFolder.DirectoryPath, "terraform.tfstate")}\" -refresh=false");
            }

            var output = await ExecuteAndReturnResult(planCommand, PopulateVariables, "PlanDetailedExitCode");
            output.OutputVariables.ContainsKey("TerraformPlanDetailedExitCode").Should().BeTrue();
            output.OutputVariables["TerraformPlanDetailedExitCode"].Value.Should().Be("2");

            output = await ExecuteAndReturnResult(applyCommand, PopulateVariables, "PlanDetailedExitCode");
            output.FullLog.Should()
                  .Contain("apply -no-color -auto-approve");

            output = await ExecuteAndReturnResult(planCommand, PopulateVariables, "PlanDetailedExitCode");
            output.OutputVariables.ContainsKey("TerraformPlanDetailedExitCode").Should().BeTrue();
            output.OutputVariables["TerraformPlanDetailedExitCode"].Value.Should().Be("0");
        }

        [Test]
        public void InlineHclTemplateAndVariables()
        {
            IgnoreIfVersionIsNotInRange("0.11.15", "0.15.0");
            const string variables = "stringvar = \"default string\"";
            string template = TemplateLoader.LoadTextTemplate("HclWithVariablesV0118.hcl");

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
        public void InlineHclTemplateAndVariablesV015()
        {
            IgnoreIfVersionIsNotInRange("0.15.0");

            const string variables = "stringvar = \"default string\"";
            string template = TemplateLoader.LoadTextTemplate("HclWithVariablesV0150.hcl");

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
        public void InlineJsonTemplateAndVariables()
        {
            IgnoreIfVersionIsNotInRange("0.11.15", "0.15.0");
            const string variables =
                "{\"ami\":\"new ami value\"}";
            string template = TemplateLoader.LoadTextTemplate("InlineJsonWithVariablesV01180.json");

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

        [Test]
        public void CanDetermineTerraformVersion()
        {
            ExecuteAndReturnLogOutput(applyCommand, _ => { _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Workspace, "testversionspace"); }, "Simple")
                .Should()
                .NotContain("Could not parse Terraform CLI version");
        }

        [Test]
        public void InlineJsonTemplateAndVariablesV015()
        {
            IgnoreIfVersionIsNotInRange("0.15.0");

            const string variables =
                "{\"ami\":\"new ami value\"}";
            string template = TemplateLoader.LoadTextTemplate("InlineJsonWithVariablesV0150.json");

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

        static void CopyAllFiles(string sourceFolderPath, string destinationFolderPath)
        {
            if (Directory.Exists(sourceFolderPath))
            {
                var filePaths = Directory.GetFiles(sourceFolderPath);

                // Copy the files and overwrite destination files if they already exist.
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
            Func<CommandTestBuilderContext, Task> wrappedAction = context =>
                                                                  {
                                                                      populateVariables(context);
                                                                      return Task.CompletedTask;
                                                                  };
            
            return ExecuteAndReturnLogOutput(command, wrappedAction, folderName, assert);
        }
        
        string ExecuteAndReturnLogOutput(string command,
                                         Func<CommandTestBuilderContext, Task> populateVariables,
                                         string folderName,
                                         Action<TestCalamariCommandResult>? assert = null)
        {
            return ExecuteAndReturnResult(command, populateVariables, folderName, assert).Result.FullLog;
        }
        

        async Task<TestCalamariCommandResult> ExecuteAndReturnResult(string command, Action<CommandTestBuilderContext> populateVariables, string folderName, Action<TestCalamariCommandResult>? assert = null)
        {
            Func<CommandTestBuilderContext, Task> wrappedAction = context =>
                                                         {
                                                             populateVariables(context);
                                                             return Task.CompletedTask;
                                                         };
            return await ExecuteAndReturnResult(command, wrappedAction, folderName, assert);
        }
        
        async Task<TestCalamariCommandResult> ExecuteAndReturnResult(string command, Func<CommandTestBuilderContext, Task> populateVariables, string folderName, Action<TestCalamariCommandResult>? assert = null)
        {
            var assertResult = assert ?? (_ => { });

            var terraformFiles = Path.IsPathRooted(folderName) ? folderName : TestEnvironment.GetTestPath(folderName);

            var result = await CommandTestBuilder.CreateAsync<Program>(command)
                                                 .WithArrange(context =>
                                                              {
                                                                  context.Variables.Add(ScriptVariables.ScriptSource,
                                                                      ScriptVariables.ScriptSourceOptions.Package);
                                                                  context.Variables.Add(TerraformSpecialVariables.Packages.PackageId, terraformFiles);
                                                                  context.Variables.Add(TerraformSpecialVariables.Calamari.TerraformCliPath,
                                                                                        Path.GetDirectoryName(customTerraformExecutable));
                                                                  context.Variables.Add(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable,
                                                                                        customTerraformExecutable);

                                                                  populateVariables(context).RunSynchronously();

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
                              ?.ToString();
        }

        void IgnoreIfVersionIsNotInRange(string minimum, string? maximum = null)
        {
            var minimumVersion = new Version(minimum);
            var maximumVersion = new Version(maximum ?? "999.0.0");

            if (TerraformCliVersionAsObject.CompareTo(minimumVersion) < 0
                || TerraformCliVersionAsObject.CompareTo(maximumVersion) >= 0)
            {
                Assert.Ignore($"Test ignored as terraform version is not between {minimumVersion} and {maximumVersion}");
            }
        }

        //TODO: This is ported over from the ExecutableHelper in Sashimi.Tests.Shared. This project doesn't have a valid nuget package for net452
        static void AddExecutePermission(string exePath)
        {
            if (CalamariEnvironment.IsRunningOnWindows)
                return;
            StringBuilder stdOut = new StringBuilder();
            StringBuilder stdError = new StringBuilder();
            if (SilentProcessRunner.ExecuteCommand("chmod",
                                                   "+x " + exePath,
                                                   Path.GetDirectoryName(exePath) ?? string.Empty,
                                                   (Action<string>)(s => stdOut.AppendLine(s)),
                                                   (Action<string>)(s => stdError.AppendLine(s)))
                                   .ExitCode
                != 0)
                throw new Exception(stdOut.ToString() + stdError?.ToString());
        }
    }
}