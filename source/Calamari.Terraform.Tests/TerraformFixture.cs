using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.CloudAccounts;
using Calamari.Commands.Support;
using Calamari.Common.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;
using Calamari.Integration.Retry;
using Calamari.Integration.Substitutions;
using Calamari.Tests.Shared;
using Calamari.Tests.Shared.Helpers;
using Calamari.Tests.Shared.Requirements;
using Calamari.Util;
using Calamari.Variables;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Octostache;

namespace Calamari.Terraform.Tests
{
    [TestFixture]
    [RequiresNonFreeBSDPlatform]
    [RequiresNon32BitWindows]
    [RequiresNonMac]
    [RequiresNonMono]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class TerraformFixture
    {
        private string customTerraformExecutable;

        [OneTimeSetUp]
        public async Task InstallTools()
        {
            async Task DownloadCli(string destination)
            {
                Console.WriteLine("Downloading terraform cli...");
                
                // Set Security Protocol to TLS1.2 (flag 3072)
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                
                var retry = new RetryTracker(3, TimeSpan.MaxValue, new LimitedExponentialRetryInterval(1000, 30000, 2));
                while (retry.Try())
                {
                    try
                    {
                        using (var client = new HttpClient())
                        {
                            var json = await client.GetStringAsync("https://checkpoint-api.hashicorp.com/v1/check/terraform");
                            var parsedJson = JObject.Parse(json);

                            var downloadBaseUrl = parsedJson["current_download_url"].Value<string>();
                            var currentVersion = parsedJson["current_version"].Value<string>();
                            var fileName = GetTerraformFileName(currentVersion);

                            if (!TerraformFileAvailable(downloadBaseUrl, retry, fileName))
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

        static string GetTerraformFileName(string currentVersion)
        {
            return CalamariEnvironment.IsRunningOnNix
                ? $"terraform_{currentVersion}_linux_amd64.zip"
                : $"terraform_{currentVersion}_windows_amd64.zip";
        }

        static bool TerraformFileAvailable(string downloadBaseUrl, RetryTracker retry, string fileName)
        {
            try
            {
                var request = WebRequest.Create($"{downloadBaseUrl}{fileName}");
                request.Method = "HEAD";

                using (request.GetResponse())
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"There was an error accessing the terraform cli on try #{retry.CurrentTry}. Falling back to default. {ex.Message}");
                return false;
            }
        }

        static async Task DownloadTerraform(string fileName, HttpClient client, string downloadBaseUrl, string destination)
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

        [Test]
        [TestCase("-backend-config=\"backend.tfvars\"", TestName = "Using double quotes")]
        [TestCase("--backend-config=backend.tfvars", TestName = "Using no quotes, this one needs to use -- for the argument!")]
        public void ExtraInitParametersAreSet(string additionalParams)
        {
            ExecuteAndReturnLogOutput<PlanCommand>(_ =>
                    _.Set(TerraformSpecialVariables.Action.Terraform.AdditionalInitParams, additionalParams), "Simple")
                .Should().Contain($"init -no-color -get-plugins=true {additionalParams}");
        }

        [Test]
        public void AllowPluginDownloadsShouldBeDisabled()
        {
            ExecuteAndReturnLogOutput<PlanCommand>(_ =>
                    _.Set(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads, false.ToString()), "Simple")
                .Should().Contain("init -no-color -get-plugins=false");
        }

        [Test]
        public void AttachLogFile()
        {
            ExecuteAndReturnLogOutput<PlanCommand>(_ =>
                    _.Set(TerraformSpecialVariables.Action.Terraform.AttachLogFile, true.ToString()), "Simple")
                .Should().Contain("##octopus[createArtifact ");
        }

        [Test]
        [TestCase(typeof(PlanCommand), "plan -no-color -detailed-exitcode -var my_var=\"Hello world\"")]
        [TestCase(typeof(ApplyCommand), "apply -no-color -auto-approve -var my_var=\"Hello world\"")]
        [TestCase(typeof(DestroyPlanCommand), "plan -no-color -detailed-exitcode -destroy -var my_var=\"Hello world\"")]
        [TestCase(typeof(DestroyCommand), "destroy -force -no-color -var my_var=\"Hello world\"")]
        public void AdditionalActionParams(Type commandType, string expected)
        {
            ExecuteAndReturnLogOutput(commandType, _ => { _.Set(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams, "-var my_var=\"Hello world\""); }, "AdditionalParams")
                .Should().Contain(expected);
        }

        [Test]
        [TestCase(typeof(PlanCommand), "plan -no-color -detailed-exitcode -var-file=\"example.tfvars\"")]
        [TestCase(typeof(ApplyCommand), "apply -no-color -auto-approve -var-file=\"example.tfvars\"")]
        [TestCase(typeof(DestroyPlanCommand), "plan -no-color -detailed-exitcode -destroy -var-file=\"example.tfvars\"")]
        [TestCase(typeof(DestroyCommand), "destroy -force -no-color -var-file=\"example.tfvars\"")]
        public void VarFiles(Type commandType, string actual)
        {
            ExecuteAndReturnLogOutput(commandType, _ => { _.Set(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars"); }, "WithVariables")
                .Should().Contain(actual);
        }

        [Test]
        public void WithOutputSensitiveVariables()
        {
            ExecuteAndReturnLogOutput<ApplyCommand>(_ => { }, "WithOutputSensitiveVariables")
                .Should().Contain("sensitive=\"");
        }

        [Test]
        public void OutputAndSubstituteOctopusVariables()
        {
            ExecuteAndReturnLogOutput<ApplyCommand>(_ =>
                {
                    _.Set(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.txt");
                    _.Set(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "example.txt");
                    _.Set("Octopus.Action.StepName", "Step Name");
                    _.Set("Should_Be_Substituted", "Hello World");
                    _.Set("Should_Be_Substituted_in_txt", "Hello World from text");
                }, "WithVariablesSubstitution")
                .Should()
                .Contain("Octopus.Action[\"Step Name\"].Output.TerraformValueOutputs[\"my_output\"]' with the value only of 'Hello World'")
                .And
                .Contain("Octopus.Action[\"Step Name\"].Output.TerraformValueOutputs[\"my_output_from_txt_file\"]' with the value only of 'Hello World from text'");
        }
        
        [Test]
        public void EnableNoMatchWarningIsNotSet()
        {
            ExecuteAndReturnLogOutput<ApplyCommand>(variables => { }, "Simple")
                .Should()
                .NotContain("No files were found that match the substitution target pattern");
        }
             
        [Test]
        public void EnableNoMatchWarningIsNotSetWithAdditionSubstitution()
        {
            ExecuteAndReturnLogOutput<ApplyCommand>(variables =>
                {
                    variables.Set(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "doesNotExist.txt");
                }, "Simple")
                .Should()
                .Contain("No files were found that match the substitution target pattern '**/*.tfvars.json'")
                .And
                .Contain("No files were found that match the substitution target pattern 'doesNotExist.txt'");
        }
        
        [Test]
        public void EnableNoMatchWarningIsTrue()
        {
            ExecuteAndReturnLogOutput<ApplyCommand>(variables =>
                {
                    variables.Set(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "doesNotExist.txt");
                    variables.Set(PackageVariables.EnableNoMatchWarning, "true");
                }, "Simple")
                .Should()
                .Contain("No files were found that match the substitution target pattern '**/*.tfvars.json'")
                .And
                .Contain("No files were found that match the substitution target pattern 'doesNotExist.txt'");
        }
        
        [Test]
        public void EnableNoMatchWarningIsFalse()
        {
            ExecuteAndReturnLogOutput<ApplyCommand>(variables =>
                {
                    variables.Set(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "doesNotExist.txt");
                    variables.Set(PackageVariables.EnableNoMatchWarning, "False");
                }, "Simple")
                .Should()
                .NotContain("No files were found that match the substitution target pattern");
        }

        [Test]
        [TestCase(typeof(PlanCommand))]
        [TestCase(typeof(DestroyPlanCommand))]
        public void TerraformPlanOutput(Type commandType)
        {
            ExecuteAndReturnLogOutput(commandType, _ => { _.Set("Octopus.Action.StepName", "Step Name"); }, "Simple")
                .Should().Contain("Octopus.Action[\"Step Name\"].Output.TerraformPlanOutput");
        }

        [Test]
        public void UsesWorkSpace()
        {
            ExecuteAndReturnLogOutput<ApplyCommand>(_ => { _.Set(TerraformSpecialVariables.Action.Terraform.Workspace, "myspace"); }, "Simple")
                .Should().Contain("workspace new \"myspace\"");
        }

        [Test]
        public void UsesTemplateDirectory()
        {
            ExecuteAndReturnLogOutput<ApplyCommand>(_ => { _.Set(TerraformSpecialVariables.Action.Terraform.TemplateDirectory, "SubFolder"); }, "TemplateDirectory")
                .Should().Contain($"SubFolder{Path.DirectorySeparatorChar}example.tf");
        }

        [Test]
        public async Task AzureIntegration()
        {
            var random = Guid.NewGuid().ToString("N").Substring(0, 6);
            var appName = $"cfe2e-{random}";

            void PopulateVariables(VariableDictionary _)
            {
                _.Set(AzureAccountVariables.SubscriptionId, ExternalVariables.Get(ExternalVariable.AzureSubscriptionId));
                _.Set(AzureAccountVariables.TenantId, ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId));
                _.Set(AzureAccountVariables.ClientId, ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId));
                _.Set(AzureAccountVariables.Password, ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword));
                _.Set("app_name", appName);
                _.Set("random", random);
                _.Set(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                _.Set(TerraformSpecialVariables.Action.Terraform.AzureManagedAccount, Boolean.TrueString);
            }

            using (var outputs = ExecuteAndReturnLogOutput(PopulateVariables, "Azure", typeof(PlanCommand),
                typeof(ApplyCommand), typeof(DestroyCommand)).GetEnumerator())
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

            void PopulateVariables(VariableDictionary _)
            {
                _.Set(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "test.txt");
                _.Set("Octopus.Action.Amazon.AccessKey", ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey));
                _.Set("Octopus.Action.Amazon.SecretKey", ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey));
                _.Set("Octopus.Action.Aws.Region", "ap-southeast-1");
                _.Set("Hello", "Hello World from AWS");
                _.Set("bucket_name", bucketName);
                _.Set(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                _.Set(TerraformSpecialVariables.Action.Terraform.AWSManagedAccount, "AWS");
            }

            using (var outputs = ExecuteAndReturnLogOutput(PopulateVariables, "AWS", typeof(PlanCommand), typeof(ApplyCommand), typeof(DestroyCommand)).GetEnumerator())
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
            using (var outputs = ExecuteAndReturnLogOutput(_ => { }, "PlanDetailedExitCode", typeof(PlanCommand), typeof(ApplyCommand), typeof(PlanCommand)).GetEnumerator())
            {
                outputs.MoveNext();
                outputs.Current.Should()
                    .Contain("Saving variable 'Octopus.Action[\"\"].Output.TerraformPlanDetailedExitCode' with the detailed exit code of the plan, with value '2'");

                outputs.MoveNext();
                outputs.Current.Should()
                    .Contain("apply -no-color -auto-approve");

                outputs.MoveNext();
                outputs.Current.Should()
                    .Contain("Saving variable 'Octopus.Action[\"\"].Output.TerraformPlanDetailedExitCode' with the detailed exit code of the plan, with value '0'");
            }
        }

        string ExecuteAndReturnLogOutput(Type commandType, Action<VariableDictionary> populateVariables, string folderName)
        {
            return ExecuteAndReturnLogOutput(populateVariables, folderName, commandType).Single();
        }

        IEnumerable<string> ExecuteAndReturnLogOutput(Action<VariableDictionary> populateVariables, string folderName, params Type[] commandTypes)
        {
            void Copy(string sourcePath, string destinationPath)
            {
                foreach (var dirPath in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(dirPath.Replace(sourcePath, destinationPath));
                }

                foreach (var newPath in Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                {
                    File.Copy(newPath, newPath.Replace(sourcePath, destinationPath), true);
                }
            }

            using (var currentDirectory = TemporaryDirectory.Create())
            {
                var variables = new CalamariVariables();
                variables.Set(TerraformSpecialVariables.Calamari.TerraformCliPath, Path.GetDirectoryName(customTerraformExecutable));
                variables.Set(KnownVariables.OriginalPackageDirectoryPath, currentDirectory.DirectoryPath);
                variables.Set(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable, customTerraformExecutable);

                populateVariables(variables);

                var terraformFiles = TestEnvironment.GetTestPath(folderName);

                Copy(terraformFiles, currentDirectory.DirectoryPath);

                foreach (var commandType in commandTypes)
                {
                    var log = new InMemoryLog();
                    var command = CreateInstance(commandType, variables, log);
                    var result = command.Execute();

                    result.Should().Be(0);

                    var output = log.StandardOut.Join(Environment.NewLine);

                    Console.WriteLine(output);

                    yield return output;
                }
            }
        }

        ICommand CreateInstance(Type type, IVariables variables, ILog log)
        {
            var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            var commandLineRunner = new CommandLineRunner(ConsoleLog.Instance, variables);
            var substituteInFiles = new SubstituteInFiles(log, fileSystem, new FileSubstituter(log, fileSystem), variables);
            var extractPackages = new ExtractPackage(new CombinedPackageExtractor(log), fileSystem, variables, log);

            if (type == typeof(PlanCommand))
                return new PlanCommand(log, variables, fileSystem, commandLineRunner, substituteInFiles, extractPackages);
            
            if (type == typeof(ApplyCommand))
                return new ApplyCommand(log, variables, fileSystem, commandLineRunner, substituteInFiles, extractPackages);

            if (type == typeof(DestroyCommand))
                return new DestroyCommand(log, variables, fileSystem, commandLineRunner, substituteInFiles, extractPackages);

            if (type == typeof(DestroyPlanCommand))
                return new DestroyPlanCommand(log, variables, fileSystem, commandLineRunner, substituteInFiles, extractPackages);
            
            throw new ArgumentException();
        }

        string ExecuteAndReturnLogOutput<T>(Action<VariableDictionary> populateVariables, string folderName) where T : ICommand
        {
            return ExecuteAndReturnLogOutput(typeof(T), populateVariables, folderName);
        }
    }
}