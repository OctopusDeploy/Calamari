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
    public class ActionHandlersFixture
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

                            var downloadBaseUrl = parsedJson["current_download_url"]!.Value<string>();
                            var currentVersion = parsedJson["current_version"]!.Value<string>();
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
                }, "WithVariablesSubstitution",
                result =>
                {
                    result.OutputVariables
                        .ContainsKey("TerraformValueOutputs[my_output]")
                        .Should().BeTrue();
                    result.OutputVariables["TerraformValueOutputs[my_output]"].Value
                        .Should()
                        .Be("Hello World");
                    result.OutputVariables
                        .ContainsKey("TerraformValueOutputs[my_output_from_txt_file]")
                        .Should().BeTrue();
                    result.OutputVariables["TerraformValueOutputs[my_output_from_txt_file]"]
                        .Value
                        .Should()
                        .Be("Hello World from text");
                });
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
            ExecuteAndReturnLogOutput(commandType, _ => { _.Variables.Add("Octopus.Action.StepName", "Step Name"); }, "Simple",
                    result =>
                    {
                        result.OutputVariables
                            .ContainsKey("TerraformPlanOutput")
                            .Should().BeTrue();
                    });
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

            using (var outputs = ExecuteAndReturnLogOutput(PopulateVariables, "Azure", typeof(TerraformPlanActionHandler),
                typeof(TerraformApplyActionHandler), typeof(TerraformDestroyActionHandler)).GetEnumerator())
            {
                outputs.MoveNext();
                outputs.Current.OutputVariables.ContainsKey("TerraformPlanOutput").Should().BeTrue();

                outputs.MoveNext();
                outputs.Current.OutputVariables.ContainsKey("TerraformValueOutputs[url]").Should().BeTrue();
                outputs.Current.OutputVariables["TerraformValueOutputs[url]"].Value.Should().Be($"{appName}.azurewebsites.net");

                using (var client = new HttpClient())
                {
                    using (var responseMessage =
                        await client.GetAsync($"https://{appName}.azurewebsites.net").ConfigureAwait(false))
                    {
                        Assert.AreEqual(HttpStatusCode.Forbidden, responseMessage.StatusCode);
                    }
                }

                outputs.MoveNext();
                outputs.Current.FullLog.Should()
                    .Contain("destroy -force -no-color");
            }
        }

        [Test]
        public async Task AWSIntegration()
        {
            var bucketName = $"cfe2e-{Guid.NewGuid().ToString("N").Substring(0, 6)}";

            var temporaryFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(temporaryFolder);
            CopyAllFiles(TestEnvironment.GetTestPath("AWS"), temporaryFolder);

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
                _.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, temporaryFolder);
            }

            using (var outputs = ExecuteAndReturnLogOutput(PopulateVariables, "AWS", typeof(TerraformPlanActionHandler), typeof(TerraformApplyActionHandler), typeof(TerraformDestroyActionHandler)).GetEnumerator())
            {
                outputs.MoveNext();
                outputs.Current.OutputVariables.ContainsKey("TerraformPlanOutput").Should().BeTrue();

                outputs.MoveNext();
                outputs.Current.OutputVariables.ContainsKey("TerraformValueOutputs[url]").Should().BeTrue();
                outputs.Current.OutputVariables["TerraformValueOutputs[url]"].Value.Should().Be($"https://{bucketName}.s3.amazonaws.com/test.txt");

                string fileData;
                using (var client = new HttpClient())
                {
                    fileData = await client.GetStringAsync($"https://{bucketName}.s3.amazonaws.com/test.txt").ConfigureAwait(false);
                }

                fileData.Should().Be("Hello World from AWS");

                outputs.MoveNext();

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync($"https://{bucketName}.s3.amazonaws.com/test.txt").ConfigureAwait(false);

                    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
                }

                Directory.Delete(temporaryFolder, true);
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
                    }, "PlanDetailedExitCode",
                    typeof(TerraformPlanActionHandler), typeof(TerraformApplyActionHandler),
                    typeof(TerraformPlanActionHandler)).GetEnumerator();
                outputs.MoveNext();
                outputs.Current.OutputVariables.ContainsKey("TerraformPlanDetailedExitCode").Should().BeTrue();
                outputs.Current.OutputVariables["TerraformPlanDetailedExitCode"].Value.Should().Be("2");

                outputs.MoveNext();
                outputs.Current.FullLog.Should()
                    .Contain("apply -no-color -auto-approve");

                outputs.MoveNext();
                outputs.Current.OutputVariables.ContainsKey("TerraformPlanDetailedExitCode").Should().BeTrue();
                outputs.Current.OutputVariables["TerraformPlanDetailedExitCode"].Value.Should().Be("0");
            }
        }

        [Test]
        public void InlineHclTemplateAndVariables()
        {
            const string variables =
                "{\"stringvar\":\"default string\",\"images\":\"\",\"test2\":\"\",\"test3\":\"\",\"test4\":\"\"}";
            const string template = @"variable stringvar {
  type = ""string""
  default = ""default string""
}
variable ""images"" {
  type = ""map""
  default = {
    us-east-1 = ""image-1234""
    us-west-2 = ""image-4567""
  }
}
variable ""test2"" {
  type    = ""map""
  default = {
    val1 = [""hi""]
  }
}
variable ""test3"" {
  type    = ""map""
  default = {
    val1 = {
      val2 = ""#{RandomNumber}""
    }
  }
}
variable ""test4"" {
  type    = ""map""
  default = {
    val1 = {
      val2 = [""hi""]
    }                        
  }
}
# Example of getting an element from a list in a map
output ""nestedlist"" {
  value = ""${element(var.test2[""val1""], 0)}""
}
# Example of getting an element from a nested map
output ""nestedmap"" {
  value = ""${lookup(var.test3[""val1""], ""val2"")}""
}";                                        

            ExecuteAndReturnLogOutput<TerraformApplyActionHandler>(_ =>
            {
                _.Variables.Add("RandomNumber", new Random().Next().ToString());
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, variables);
                _.Variables.Add(KnownVariables.Action.Script.ScriptSource,
                    KnownVariables.Action.Script.ScriptSourceOptions.Inline);
            }, String.Empty, _ =>
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
            string template = String.Format(@"locals {{
  config-map-aws-auth = <<CONFIGMAPAWSAUTH
{0}
CONFIGMAPAWSAUTH
}}

output ""config-map-aws-auth"" {{
    value = ""${{local.config-map-aws-auth}}""
}}", expected);                                     

            ExecuteAndReturnLogOutput<TerraformApplyActionHandler>(_ =>
            {
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, "{}");
                _.Variables.Add(KnownVariables.Action.Script.ScriptSource,
                    KnownVariables.Action.Script.ScriptSourceOptions.Inline);
            }, String.Empty, _ =>
            {
                _.OutputVariables.ContainsKey("TerraformValueOutputs[config-map-aws-auth]").Should().BeTrue();
                _.OutputVariables["TerraformValueOutputs[config-map-aws-auth]"].Value.Should().Be(
                    $"{expected}{Environment.NewLine}");
            });
        }

        [Test]
        public void InlineJsonTemplateAndVariables()
        {
            const string variables =
                "{\"ami\":\"new ami value\"}";
            const string template = @"{
    ""variable"":{
      ""ami"":{
         ""type"":""string"",
         ""description"":""the AMI to use"",
         ""default"":""1234567890""
      }
    },
    ""output"":{
      ""test"":{
         ""value"":""hi there""
      },
      ""test2"":{
         ""value"":[
            ""hi there"",
            ""hi again""
         ]
      },
      ""test3"":{
         ""value"":""${map(\""a\"", \""hi\"")}""
      },
      ""ami"":{
         ""value"":""${var.ami}""
      },
      ""random"":{
         ""value"":""#{RandomNumber}""
      }
    }
}";                          

            var randomNumber = new Random().Next().ToString();

            ExecuteAndReturnLogOutput<TerraformApplyActionHandler>(_ =>
            {
                _.Variables.Add("RandomNumber", randomNumber);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, variables);
                _.Variables.Add(KnownVariables.Action.Script.ScriptSource,
                    KnownVariables.Action.Script.ScriptSourceOptions.Inline);
            }, String.Empty, _ =>
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

        string ExecuteAndReturnLogOutput(Type commandType, Action<TestActionHandlerContext<Program>> populateVariables,
            string folderName, Action<TestActionHandlerResult>? assert = null)
        {
            var assertResult = assert ?? (_ => { });

            var result = ExecuteAndReturnLogOutput(populateVariables, folderName, commandType).Single();

            assertResult(result);

            return result.FullLog;
        }

        string ExecuteAndReturnLogOutput<T>(Action<TestActionHandlerContext<Program>> populateVariables,
            string folderName, Action<TestActionHandlerResult>? assert = null) where T : IActionHandler
        {
            return ExecuteAndReturnLogOutput(typeof(T), populateVariables, folderName, assert);
        }

        IEnumerable<TestActionHandlerResult> ExecuteAndReturnLogOutput(Action<TestActionHandlerContext<Program>> populateVariables,
            string folderName, params Type[] commandTypes)
        {
            var terraformFiles = TestEnvironment.GetTestPath(folderName);
            
            foreach (var commandType in commandTypes)
            {
                yield return ActionHandlerTestBuilder.Create<Program>(commandType)
                    .WithArrange(context =>
                    {
                        context.Variables.Add(KnownVariables.Action.Script.ScriptSource,
                            KnownVariables.Action.Script.ScriptSourceOptions.Package);
                        context.Variables.Add(KnownVariables.Action.Packages.PackageId, terraformFiles);
                        context.Variables.Add(TerraformSpecialVariables.Calamari.TerraformCliPath,
                            Path.GetDirectoryName(customTerraformExecutable));
                        context.Variables.Add(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable,
                            customTerraformExecutable);

                        populateVariables(context);
                    })
                    .WithAssert(result =>
                    {
                        Assert.IsTrue(result.WasSuccessful);
                    })
                    .Execute();
            }
        }
    }
}