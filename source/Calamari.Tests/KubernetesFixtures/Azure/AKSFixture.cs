using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Retry;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Tests.KubernetesFixtures.Azure
{
    public static class ExecutableHelper
    {
        public static void AddExecutePermission(string exePath)
        {
            if (CalamariEnvironment.IsRunningOnWindows)
                return;

            var stdOut = new StringBuilder();
            var stdError = new StringBuilder();
            var result = SilentProcessRunner.ExecuteCommand("chmod",
                                                            $"+x {exePath}",
                                                            Path.GetDirectoryName(exePath) ?? string.Empty,
                                                            s => stdOut.AppendLine(s),
                                                            s => stdError.AppendLine(s));

            if (result.ExitCode != 0)
                throw new Exception(stdOut.ToString() + stdError);
        }
    }

    [TestFixture]
    public class AKSFixture
    {
        string terraformExecutablePath;
        string terraformCliVersion = "0.15.4";
        string terraformWorkingDirectory;
        protected IVariables Variables { get; set; } = new CalamariVariables();

        public async Task InstallTools()
        {
            string GetTerraformFileName(string currentVersion)
            {
                if (CalamariEnvironment.IsRunningOnNix)
                    return $"terraform_{currentVersion}_linux_amd64.zip";
                if (CalamariEnvironment.IsRunningOnMac)
                    return $"terraform_{currentVersion}_darwin_amd64.zip";

                return $"terraform_{currentVersion}_windows_amd64.zip";
            }

            async Task DownloadTerraform(string fileName,
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

                        terraformExecutablePath = Directory.EnumerateFiles(destination).FirstOrDefault();
                        Console.WriteLine($"Downloaded terraform to {terraformExecutablePath}");

                        ExecutableHelper.AddExecutePermission(terraformExecutablePath);
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
                    terraformExecutablePath = path;
                    Console.WriteLine($"Using existing terraform located in {terraformExecutablePath}");
                    return;
                }
            }

            await DownloadCli(destinationDirectoryName, terraformCliVersion);
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

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await InstallTools();

            Environment.SetEnvironmentVariable("ARM_SUBSCRIPTION_ID", ExternalVariables.Get(ExternalVariable.AzureSubscriptionId));
            Environment.SetEnvironmentVariable("ARM_TENANT_ID", ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId));
            Environment.SetEnvironmentVariable("ARM_CLIENT_ID", ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId));
            Environment.SetEnvironmentVariable("ARM_CLIENT_SECRET", ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword));

            terraformWorkingDirectory = TemporaryDirectory.Create().DirectoryPath;

            ConsoleLog.Instance.Info($"Using temporary folder of '${terraformWorkingDirectory}'");

            CopyAllFiles(Path.Combine("KubernetesFixtures", "Azure"), terraformWorkingDirectory);

            RunTerraformInit(terraformWorkingDirectory);

            try
            {
                RunTerraformApply(terraformWorkingDirectory);
            }
            catch (Exception)
            {
                try
                {
                    RunTerraformDestroy(terraformWorkingDirectory);                    
                }
                catch (Exception destroyEx)
                {
                    ConsoleLog.Instance.Error($"An error has occurred destroying terraform resources during test fixture setup, resources may not be cleaned up correctly -> ${destroyEx}");
                }

                throw; // Rethrow the original exception so we don't lose it
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            try
            {
                RunTerraformDestroy(terraformWorkingDirectory);
            }
            catch (Exception destroyEx)
            {
                ConsoleLog.Instance.Error($"An error has occurred destroying terraform resources during test fixture teardown, resources may not be cleaned up correctly -> ${destroyEx}");
                throw;
            }            
        }

        [Test]
        public void PowershellKubeCtlScripts()
        {
            Variables.Set(Deployment.SpecialVariables.Account.AccountType, "AzureServicePrincipal");
            Variables.Set(Kubernetes.SpecialVariables.AksClusterName, "calamari-test-aks");
            Variables.Set("Octopus.Action.Kubernetes.AksClusterResourceGroup", "calamari-test-rg");
            Variables.Set(Deployment.SpecialVariables.Action.Azure.SubscriptionId, ExternalVariables.Get(ExternalVariable.AzureSubscriptionId));
            Variables.Set(Deployment.SpecialVariables.Action.Azure.TenantId, ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId));
            Variables.Set(Deployment.SpecialVariables.Action.Azure.ClientId, ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId));
            Variables.Set(Deployment.SpecialVariables.Action.Azure.Password, ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword));
            Variables.Set(ScriptVariables.Syntax, ScriptSyntax.PowerShell.ToString());
            Variables.Set(PowerShellVariables.Edition, "Core");
            var wrapper = new KubernetesContextScriptWrapper(Variables);
            TestScript(wrapper, "Test-Script.ps1");
        }

        void TestScript(IScriptWrapper wrapper, string scriptName)
        {
            using (var dir = TemporaryDirectory.Create())
            using (var temp = new TemporaryFile(Path.Combine(dir.DirectoryPath, scriptName)))
            {
                File.WriteAllText(temp.FilePath, "kubectl get nodes");

                var output = ExecuteScript(wrapper, temp.FilePath, Variables);
                output.AssertSuccess();
            }
        }

        CalamariResult ExecuteScript(IScriptWrapper wrapper, string scriptName, IVariables variables)
        {
            var runner = new TestCommandLineRunner(ConsoleLog.Instance, variables);
            var engine = new ScriptEngine(new[] { wrapper });
            var result = engine.Execute(new Script(scriptName), variables, runner, new Dictionary<string, string>());
            return new CalamariResult(result.ExitCode, runner.Output);
        }

        private void RunTerraformApply(string workingDirectory)
        {
            RunTerraformCommand("apply -auto-approve -no-color", workingDirectory);
        }

        private void RunTerraformInit(string workingDirectory)
        {
            RunTerraformCommand("init -no-color", workingDirectory);
        }

        private void RunTerraformDestroy(string workingDirectory)
        {
            RunTerraformCommand("destroy -auto-approve -no-color", workingDirectory);
        }

        private void RunTerraformCommand(string command, string workingDirectory)
        {
            var stdOut = new StringBuilder();
            var stdError = new StringBuilder();
            var result = SilentProcessRunner.ExecuteCommand(terraformExecutablePath, command, workingDirectory, s => stdOut.AppendLine(s), s => stdError.AppendLine(s));

            if (result.ExitCode != 0)
                throw new Exception(stdOut.ToString() + stdError.ToString());
            else
                ConsoleLog.Instance.Info(stdOut.ToString());
        }
    }
}
