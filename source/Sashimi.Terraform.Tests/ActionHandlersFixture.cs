using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari;
using Calamari.CloudAccounts;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Retry;
using Calamari.Terraform;
using Calamari.Tests.Shared;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Terraform.ActionHandler;
using Sashimi.Tests.Shared;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.Terraform.Tests
{
    [TestFixture]
    public class ActionHandlersFixture : BaseTest
    {
        string? customTerraformExecutable;

        [OneTimeSetUp]
        public async Task InstallTools()
        {
            static string GetTerraformFileName(string currentVersion)
            {
                return CalamariEnvironment.IsRunningOnNix
                    ? $"terraform_{currentVersion}_linux_amd64.zip"
                    : $"terraform_{currentVersion}_windows_amd64.zip";
            }

            static async Task<bool> TerraformFileAvailable(string downloadBaseUrl, RetryTracker retry)
            {
                try
                {
                    HttpClient client = new HttpClient();

                    var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, downloadBaseUrl),
                        HttpCompletionOption.ResponseHeadersRead);

                    response.EnsureSuccessStatusCode();

                    using (response)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(
                        $"There was an error accessing the terraform cli on try #{retry.CurrentTry}. Falling back to default. {ex.Message}");
                    return false;
                }
            }

            static async Task DownloadTerraform(string fileName, HttpClient client, string downloadBaseUrl,
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

            async Task DownloadCli(string destination)
            {
                Console.WriteLine("Downloading terraform cli...");

                var retry = new RetryTracker(3, TimeSpan.MaxValue, new LimitedExponentialRetryInterval(1000, 30000, 2));
                while (retry.Try())
                {
                    try
                    {
                        using (var client = new HttpClient())
                        {
                            var json = await client.GetStringAsync(
                                "https://checkpoint-api.hashicorp.com/v1/check/terraform");
                            var parsedJson = JObject.Parse(json);

                            var downloadBaseUrl = parsedJson["current_download_url"].Value<string>();
                            var currentVersion = parsedJson["current_version"].Value<string>();
                            var fileName = GetTerraformFileName(currentVersion);

                            if (!await TerraformFileAvailable(downloadBaseUrl, retry))
                            {
                                // At times Terraform's API has been unreliable. This is a fallback
                                // for a version we know exists.
                                downloadBaseUrl = "https://releases.hashicorp.com/terraform/0.12.19/";
                                currentVersion = "0.12.19";
                                fileName = GetTerraformFileName(currentVersion);
                            }

                            await DownloadTerraform(fileName, client, downloadBaseUrl, destination);
                        }

                        customTerraformExecutable = Directory.EnumerateFiles(destination).FirstOrDefault();
                        Console.WriteLine($"Downloaded terraform to {customTerraformExecutable}");

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

            var destinationDirectoryName = TestEnvironment.GetTestPath("TerraformCLIPath");

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

            await DownloadCli(destinationDirectoryName);
        }

        [Test]
        public void ExtraInitParametersAreSet()
        {
            var additionalParams = "-var-file=\"backend.tfvars\"";
            ExecuteAndReturnLogOutput<TerraformPlanActionHandler>(_ =>
                    _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AdditionalInitParams, additionalParams), "Simple")
                .Should().Contain($"init -no-color -get-plugins=true {additionalParams}");
        }
        
        [Test]
        public void AllowPluginDownloadsShouldBeDisabled()
        {
            ExecuteAndReturnLogOutput<TerraformPlanActionHandler>(
                _ =>
                {
                    _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads,
                        false.ToString());
                }, "Simple").Should().Contain("init -no-color -get-plugins=false");
        }
        
        [Test]
        public void AttachLogFile()
        {
            ExecuteAndReturnLogOutput<TerraformPlanActionHandler>(_ =>
                    _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AttachLogFile, true.ToString()),
                "Simple",
                result =>
                {
                    result.Artifacts.Count.Should().Be(1);
                });
        }

        [Test]
        [TestCase(typeof(TerraformPlanActionHandler), "plan -no-color -detailed-exitcode -var my_var=\"Hello world\"")]
        [TestCase(typeof(TerraformApplyActionHandler), "apply -no-color -auto-approve -var my_var=\"Hello world\"")]
        [TestCase(typeof(TerraformPlanDestroyActionHandler), "plan -no-color -detailed-exitcode -destroy -var my_var=\"Hello world\"")]
        [TestCase(typeof(TerraformDestroyActionHandler), "destroy -force -no-color -var my_var=\"Hello world\"")]
        public void AdditionalActionParams(Type commandType, string expected)
        {
            ExecuteAndReturnLogOutput(commandType, _ => { _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams, "-var my_var=\"Hello world\""); }, "AdditionalParams")
                .Should().Contain(expected);
        }

        [Test]
        [TestCase(typeof(TerraformPlanActionHandler), "plan -no-color -detailed-exitcode -var-file=\"example.tfvars\"")]
        [TestCase(typeof(TerraformApplyActionHandler), "apply -no-color -auto-approve -var-file=\"example.tfvars\"")]
        [TestCase(typeof(TerraformPlanDestroyActionHandler), "plan -no-color -detailed-exitcode -destroy -var-file=\"example.tfvars\"")]
        [TestCase(typeof(TerraformDestroyActionHandler), "destroy -force -no-color -var-file=\"example.tfvars\"")]
        public void VarFiles(Type commandType, string actual)
        {
            ExecuteAndReturnLogOutput(commandType, _ => { _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars"); }, "WithVariables")
                .Should().Contain(actual);
        }

        [Test]
        public void WithOutputSensitiveVariables()
        {
            ExecuteAndReturnLogOutput<TerraformApplyActionHandler>(_ => { }, "WithOutputSensitiveVariables",
                result =>
                {
                    result.OutputVariables.Values.Should().OnlyContain(variable => variable.IsSensitive);
                });
        }

        [Test]
        public void OutputAndSubstituteOctopusVariables()
        {
            ExecuteAndReturnLogOutput<TerraformApplyActionHandler>(_ =>
                {
                    _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.txt");
                    _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "example.txt");
                    _.Variables.Add("Octopus.Action.StepName", "Step Name");
                    _.Variables.Add("Should_Be_Substituted", "Hello World");
                    _.Variables.Add("Should_Be_Substituted_in_txt", "Hello World from text");
                }, "WithVariablesSubstitution")
                .Should()
                .Contain("Octopus.Action[\"Step Name\"].Output.TerraformValueOutputs[\"my_output\"]' with the value only of 'Hello World'")
                .And
                .Contain("Octopus.Action[\"Step Name\"].Output.TerraformValueOutputs[\"my_output_from_txt_file\"]' with the value only of 'Hello World from text'");
        }
        
        [Test]
        public void EnableNoMatchWarningIsNotSet()
        {
            ExecuteAndReturnLogOutput<TerraformApplyActionHandler>(variables => { }, "Simple")
                .Should()
                .NotContain("No files were found that match the substitution target pattern");
        }
             
        [Test]
        public void EnableNoMatchWarningIsNotSetWithAdditionSubstitution()
        {
            ExecuteAndReturnLogOutput<TerraformApplyActionHandler>(_ =>
                {
                    _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "doesNotExist.txt");
                }, "Simple")
                .Should()
                .Contain("No files were found that match the substitution target pattern '**/*.tfvars.json'")
                .And
                .Contain("No files were found that match the substitution target pattern 'doesNotExist.txt'");
        }
        
        [Test]
        public void EnableNoMatchWarningIsTrue()
        {
            ExecuteAndReturnLogOutput<TerraformApplyActionHandler>(_ =>
                {
                    _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "doesNotExist.txt");
                    _.Variables.Add(KnownVariables.Action.SubstituteInFiles.EnableNoMatchWarning, "true");
                }, "Simple")
                .Should()
                .Contain("No files were found that match the substitution target pattern '**/*.tfvars.json'")
                .And
                .Contain("No files were found that match the substitution target pattern 'doesNotExist.txt'");
        }
        
        [Test]
        public void EnableNoMatchWarningIsFalse()
        {
            ExecuteAndReturnLogOutput<TerraformApplyActionHandler>(_ =>
                {
                    _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "doesNotExist.txt");
                    _.Variables.Add(KnownVariables.Action.SubstituteInFiles.EnableNoMatchWarning, "False");
                }, "Simple")
                .Should()
                .NotContain("No files were found that match the substitution target pattern");
        }

        [Test]
        [TestCase(typeof(TerraformPlanActionHandler))]
        [TestCase(typeof(TerraformPlanDestroyActionHandler))]
        public void TerraformPlanOutput(Type commandType)
        {
            ExecuteAndReturnLogOutput(commandType, _ => { _.Variables.Add("Octopus.Action.StepName", "Step Name"); }, "Simple")
                .Should().Contain("Octopus.Action[\"Step Name\"].Output.TerraformPlanOutput");
        }

        [Test]
        public void UsesWorkSpace()
        {
            ExecuteAndReturnLogOutput<TerraformApplyActionHandler>(_ => { _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Workspace, "myspace"); }, "Simple")
                .Should().Contain("workspace new \"myspace\"");
        }

        [Test]
        public void UsesTemplateDirectory()
        {
            ExecuteAndReturnLogOutput<TerraformApplyActionHandler>(_ => { _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateDirectory, "SubFolder"); }, "TemplateDirectory")
                .Should().Contain($"SubFolder{Path.DirectorySeparatorChar}example.tf");
        }

        [Test]
        public async Task AzureIntegration()
        {
            var random = Guid.NewGuid().ToString("N").Substring(0, 6);
            var appName = $"cfe2e-{random}";

            void PopulateVariables(TestActionHandlerContext<Program> _)
            {
                _.Variables.Add(AzureAccountVariables.SubscriptionId, ExternalVariables.Get(ExternalVariable.AzureSubscriptionId));
                _.Variables.Add(AzureAccountVariables.TenantId, ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId));
                _.Variables.Add(AzureAccountVariables.ClientId, ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId));
                _.Variables.Add(AzureAccountVariables.Password, ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword));
                _.Variables.Add("app_name", appName);
                _.Variables.Add("random", random);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AzureManagedAccount, Boolean.TrueString);
            }

            using (var outputs = ExecuteAndReturnLogOutput(PopulateVariables, "Azure", null, typeof(TerraformPlanActionHandler),
                typeof(TerraformApplyActionHandler), typeof(TerraformDestroyActionHandler)).GetEnumerator())
            {
                outputs.MoveNext();
                outputs.Current.Should()
                    .Contain("Octopus.Action[\"\"].Output.TerraformPlanOutput");

                outputs.MoveNext();
                outputs.Current.Should()
                    .Contain($"Saving variable 'Octopus.Action[\"\"].Output.TerraformValueOutputs[\"url\"]' with the value only of '{appName}.azurewebsites.net'");

                using (var client = new HttpClient())
                {
                    using (var responseMessage =
                        await client.GetAsync($"https://{appName}.azurewebsites.net").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.Forbidden, responseMessage.StatusCode);
                    }
                }

                outputs.MoveNext();
                outputs.Current.Should()
                    .Contain("destroy -force -no-color");
            }
        }

        [Test]
        public async Task AWSIntegration()
        {
            var bucketName = $"cfe2e-{Guid.NewGuid().ToString("N").Substring(0, 6)}";

            void PopulateVariables(TestActionHandlerContext<Program> _)
            {
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "test.txt");
                _.Variables.Add("Octopus.Action.Amazon.AccessKey", ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey));
                _.Variables.Add("Octopus.Action.Amazon.SecretKey", ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey));
                _.Variables.Add("Octopus.Action.Aws.Region", "ap-southeast-1");
                _.Variables.Add("Hello", "Hello World from AWS");
                _.Variables.Add("bucket_name", bucketName);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AWSManagedAccount, "AWS");
            }

            using (var outputs = ExecuteAndReturnLogOutput(PopulateVariables, "AWS", null, typeof(TerraformPlanActionHandler), typeof(TerraformApplyActionHandler), typeof(TerraformDestroyActionHandler)).GetEnumerator())
            {
                outputs.MoveNext();
                outputs.Current.Should()
                    .Contain("Octopus.Action[\"\"].Output.TerraformPlanOutput");

                outputs.MoveNext();
                outputs.Current.Should()
                    .Contain($"Saving variable 'Octopus.Action[\"\"].Output.TerraformValueOutputs[\"url\"]' with the value only of 'https://{bucketName}.s3.amazonaws.com/test.txt'");

                string fileData;
                using (var client = new HttpClient())
                {
                    fileData = await client.GetStringAsync($"https://{bucketName}.s3.amazonaws.com/test.txt").ConfigureAwait(false);
                }

                fileData.Should().Be("Hello World from AWS");

                outputs.MoveNext();
                outputs.Current.Should()
                    .Contain("destroy -force -no-color");
            }
        }

        [Test]
        public void PlanDetailedExitCode()
        {
            using (var stateFileFolder = TemporaryDirectory.Create())
            {
                using var outputs = ExecuteAndReturnLogOutput(
                    _ =>
                    {
                        _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams,
                            $"-state=\"{Path.Combine(stateFileFolder.DirectoryPath, "terraform.tfstate")}\" -refresh=false");
                    }, "PlanDetailedExitCode", null,
                    typeof(TerraformPlanActionHandler), typeof(TerraformApplyActionHandler),
                    typeof(TerraformPlanActionHandler)).GetEnumerator();
                outputs.MoveNext();
                outputs.Current.Should()
                    .Contain(
                        "Saving variable 'Octopus.Action[\"\"].Output.TerraformPlanDetailedExitCode' with the detailed exit code of the plan, with value '2'");

                outputs.MoveNext();
                outputs.Current.Should()
                    .Contain("apply -no-color -auto-approve");

                outputs.MoveNext();
                outputs.Current.Should()
                    .Contain(
                        "Saving variable 'Octopus.Action[\"\"].Output.TerraformPlanDetailedExitCode' with the detailed exit code of the plan, with value '0'");
            }
        }

        [Test]
        public void InlineTemplateAndVariables()
        {
            ExecuteAndReturnLogOutput<TerraformPlanActionHandler>(_ =>
            {
                var template = $@"output ""my_output"" {{
  value = ""boo""
}}";
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, "{}");
                _.Variables.Add(KnownVariables.Action.Script.ScriptSource, KnownVariables.Action.Script.ScriptSourceOptions.Inline);
            }, String.Empty, _ =>
            {
                _.WasSuccessful.Should().BeTrue();
            });
        }

        string ExecuteAndReturnLogOutput(Type commandType, Action<TestActionHandlerContext<Program>> populateVariables,
            string folderName, Action<TestActionHandlerResult>? assert = null)
        {
            return ExecuteAndReturnLogOutput(populateVariables, folderName, assert, commandType).Single();
        }

        string ExecuteAndReturnLogOutput<T>(Action<TestActionHandlerContext<Program>> populateVariables,
            string folderName, Action<TestActionHandlerResult>? assert = null) where T : IActionHandler
        {
            return ExecuteAndReturnLogOutput(typeof(T), populateVariables, folderName, assert);
        }

        IEnumerable<string> ExecuteAndReturnLogOutput(Action<TestActionHandlerContext<Program>> populateVariables,
            string folderName, Action<TestActionHandlerResult>? assert, params Type[] commandTypes)
        {
            var terraformFiles = TestEnvironment.GetTestPath(folderName);
            var assertResult = assert ?? (_ => { });
            
            foreach (var commandType in commandTypes)
            {
                var output = String.Empty;
                TestActionHandler<Program>(commandType, context =>
                    {
                        context.Variables.Add(KnownVariables.Action.Script.ScriptSource,
                            KnownVariables.Action.Script.ScriptSourceOptions.Package);
                        context.Variables.Add(KnownVariables.Action.Packages.PackageId, terraformFiles);
                        context.Variables.Add(TerraformSpecialVariables.Calamari.TerraformCliPath,
                            Path.GetDirectoryName(customTerraformExecutable));
                        context.Variables.Add(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable,
                            customTerraformExecutable);

                        populateVariables(context);
                    }, result =>
                    {
                        Assert.IsTrue(result.WasSuccessful);
                        assertResult(result);
                        output = result.FullLog;
                    }
                );

                Console.WriteLine(output);

                yield return output;
            }
        }
    }
}