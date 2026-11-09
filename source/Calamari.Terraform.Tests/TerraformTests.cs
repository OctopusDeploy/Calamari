using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Retry;
using Calamari.Common.Plumbing.Variables;
using Calamari.Terraform.Commands;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Calamari.Terraform.Tests;

public abstract class TerraformTests
{
    string? customTerraformExecutable;
    protected string CliVersion { get; }
    protected Version TerraformCliVersionAsObject => new(CliVersion);
    
    protected readonly string PlanCommand = GetCommandFromType(typeof(PlanCommand));
    protected readonly string ApplyCommand = GetCommandFromType(typeof(ApplyCommand));
    protected readonly string DestroyCommand = GetCommandFromType(typeof(DestroyCommand));

    protected TerraformTests(string cliVersion)
    {
        CliVersion = cliVersion;
        InstallTools().GetAwaiter().GetResult();
    }

    [OneTimeTearDown]
    public static void OneTimeTearDown()
    {
        ClearTestDirectories();
    }

    protected string ExecuteAndReturnLogOutput(string command,
                                               Action<CommandTestBuilderContext> populateVariables,
                                               string folderName,
                                               Action<TestCalamariCommandResult>? assert = null)
    {
        return ExecuteAndReturnResult(command, populateVariables, folderName, assert).Result.FullLog;
    }

    protected async Task<TestCalamariCommandResult> ExecuteAndReturnResult(string command, Action<CommandTestBuilderContext> populateVariables, string folderName, Action<TestCalamariCommandResult>? assert = null)
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

    async Task InstallTools()
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

                await ZipFile.ExtractToDirectoryAsync(zipPath, destination);
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

        var destinationDirectoryName = Path.Combine(TestEnvironment.GetTestPath("TerraformCLIPath"), CliVersion);

        if (Directory.Exists(destinationDirectoryName))
        {
            var path = Directory.EnumerateFiles(destinationDirectoryName).FirstOrDefault(f => Path.GetFileName(f).Contains("terraform"));
            if (path != null)
            {
                customTerraformExecutable = path;
                Console.WriteLine($"Using existing terraform located in {customTerraformExecutable}");
                return;
            }
        }

        await DownloadCli(destinationDirectoryName, CliVersion);
    }

    protected static void CopyAllFiles(string sourceFolderPath, string destinationFolderPath, string terraformVersion = null)
    {
        if (Directory.Exists(sourceFolderPath))
        {
            //if there is version specific folder, use that 
            if (terraformVersion != null && Directory.Exists(Path.Combine(sourceFolderPath, terraformVersion)))
            {
                sourceFolderPath = Path.Combine(sourceFolderPath, terraformVersion);
            }

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

    protected void IgnoreIfVersionIsNotInRange(string minimum, string maximum, string because)
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

    static void AddExecutePermission(string exePath)
    {
        if (CalamariEnvironment.IsRunningOnWindows)
            return;

        var stdOut = new StringBuilder();
        var stdError = new StringBuilder();
        if (SilentProcessRunner.ExecuteCommand("chmod",
                                   "+x " + exePath,
                                   Path.GetDirectoryName(exePath) ?? string.Empty,
                                   (Action<string>)(s => stdOut.AppendLine(s)),
                                   (Action<string>)(s => stdError.AppendLine(s)))
                               .ExitCode
            != 0)
            throw new Exception(stdOut.ToString() + stdError?.ToString());
    }

    protected static string GetCommandFromType(Type commandType)
    {
        return commandType.CustomAttributes.Where(t => t.AttributeType == typeof(Calamari.Common.Commands.CommandAttribute))
                          .Select(c => c.ConstructorArguments.First().Value)
                          .Single()
                          ?.ToString();
    }
}