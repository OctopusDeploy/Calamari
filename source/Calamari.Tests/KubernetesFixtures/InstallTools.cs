using System;
using System.Collections.Generic;
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
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Calamari.Tests.KubernetesFixtures
{
    public class InstallTools
    {
        readonly Action<string> log;

        public InstallTools(Action<string> log)
        {
            this.log = log;
        }

        public string TerraformExecutable { get; private set; }
        public string KubectlExecutable { get; private set; }
        public string AwsAuthenticatorExecutable { get; private set; }
        public string AwsCliExecutable { get; private set; }
        public string KubeloginExecutable { get; private set; }
        public string GcloudExecutable { get; private set; }
        public string GkeGCloudAuthPluginExecutable { get; private set; }

        public async Task Install()
        {
            await InstallTerraform();
            await InstallKubectl();
        }

        public async Task InstallTerraform()
        {
            using (var client = new HttpClient())
            {
                TerraformExecutable = await DownloadCli("Terraform",
                                                        async () =>
                                                        {
                                                            var json = await client.GetAsync("https://checkpoint-api.hashicorp.com/v1/check/terraform");
                                                            json.EnsureSuccessStatusCode();
                                                            var jObject = JObject.Parse(await json.Content.ReadAsStringAsync());
                                                            var downloadBaseUrl = jObject["current_download_url"].Value<string>();
                                                            var version = jObject["current_version"].Value<string>();
                                                            return (version, downloadBaseUrl);
                                                        },
                                                        async (destinationDirectoryName, tuple) =>
                                                        {
                                                            var fileName = GetTerraformFileName(tuple.version);

                                                            await DownloadTerraform(fileName, client, tuple.data, destinationDirectoryName);

                                                            var terraformExecutable = Directory.EnumerateFiles(destinationDirectoryName).FirstOrDefault();
                                                            return terraformExecutable;
                                                        });
            }
        }

        public async Task InstallKubectl()
        {
            using (var client = new HttpClient())
            {
                KubectlExecutable = await DownloadCli("Kubectl",
                                                      async () =>
                                                      {
                                                          var message = await client.GetAsync("https://storage.googleapis.com/kubernetes-release/release/stable.txt");
                                                          message.EnsureSuccessStatusCode();
                                                          return (await message.Content.ReadAsStringAsync(), null);
                                                      },
                                                      async (destinationDirectoryName, tuple) =>
                                                      {
                                                          var downloadUrl = GetKubectlDownloadLink(tuple.version);

                                                          await Download(Path.Combine(destinationDirectoryName, GetKubectlFileName()),
                                                                         client,
                                                                         downloadUrl);

                                                          var kubectlExecutable = Directory.EnumerateFiles(destinationDirectoryName).FirstOrDefault();
                                                          return kubectlExecutable;
                                                      });
            }
        }

        public async Task InstallAwsAuthenticator()
        {
            using (var client = new HttpClient())
            {
                AwsAuthenticatorExecutable = await DownloadCli("aws-iam-authenticator",
                                                               async () =>
                                                               {
                                                                   string requiredVersion = "v0.5.9";
                                                                   client.DefaultRequestHeaders.Add("User-Agent", "Octopus");
                                                                   client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", ExternalVariables.Get(ExternalVariable.GitHubRateLimitingPersonalAccessToken));
                                                                   var json = await client.GetAsync(
                                                                                                    $"https://api.github.com/repos/kubernetes-sigs/aws-iam-authenticator/releases/tags/{requiredVersion}");
                                                                   json.EnsureSuccessStatusCode();
                                                                   var jObject = JObject.Parse(await json.Content.ReadAsStringAsync());
                                                                   var downloadUrl =
                                                                       jObject["assets"]
                                                                           .Children()
                                                                           .FirstOrDefault(token =>
                                                                                               token["name"].Value<string>().EndsWith(GetAWSAuthenticatorFileNameEndsWith()))?[
                                                                                                                                                                               "browser_download_url"]
                                                                           .Value<string>();
                                                                   return (requiredVersion, downloadUrl);
                                                               },
                                                               async (destinationDirectoryName, tuple) =>
                                                               {
                                                                   await Download(Path.Combine(destinationDirectoryName, GetAWSAuthenticatorFileName()),
                                                                                  client,
                                                                                  tuple.data);

                                                                   var terraformExecutable = Directory.EnumerateFiles(destinationDirectoryName).FirstOrDefault();
                                                                   return terraformExecutable;
                                                               });
            }
        }

        // Note this only installs and extracts for Windows
        public async Task InstallAwsCli()
        {
            using (var client = new HttpClient())
            {
                AwsCliExecutable = await DownloadCli("aws",
                                                     () =>
                                                     {
                                                         var version = "2.11.22";
                                                         return Task.FromResult((version, GetAwsCliDownloadLink(version)));
                                                     },
                                                     async (destinationDirectoryName, tuple) =>
                                                     {
                                                         await Download(Path.Combine(destinationDirectoryName, GetAWSCliFileName()),
                                                                        client,
                                                                        tuple.data);

                                                         var awsInstaller = Directory.EnumerateFiles(destinationDirectoryName).FirstOrDefault();

                                                         if (CalamariEnvironment.IsRunningOnWindows)
                                                         {
                                                             ExecuteCommandAndReturnResult("msiexec",
                                                                                           $"/a {awsInstaller} /qn TARGETDIR={destinationDirectoryName}\\extract",
                                                                                           destinationDirectoryName);
                                                         }
                                                         else if (CalamariEnvironment.IsRunningOnNix && !CalamariEnvironment.IsRunningOnMono)
                                                         {
                                                             ExecuteCommandAndReturnResult("sudo",
                                                                                           "apt-get install unzip",
                                                                                           destinationDirectoryName);

                                                             ExecuteCommandAndReturnResult("unzip",
                                                                                           $"{Path.Combine(destinationDirectoryName, GetAWSCliFileName())} -d {destinationDirectoryName}",
                                                                                           destinationDirectoryName);
                                                         }

                                                         return !string.IsNullOrWhiteSpace(destinationDirectoryName)
                                                             ? GetAwsCliExecutablePath(destinationDirectoryName)
                                                             : string.Empty;
                                                     });
            }
        }

        public async Task InstallGCloud()
        {
            using (var client = new HttpClient())
            {
                GcloudExecutable = await DownloadCli("gcloud",
                                                     () => Task.FromResult<(string, string)>(("436.0.0", string.Empty)),
                                                     async (destinationDirectoryName, tuple) =>
                                                     {
                                                         var downloadUrl = GetGcloudDownloadLink(tuple.version);
                                                         var fileName = GetGcloudZipFileName(tuple.version);

                                                         await DownloadAndExtractToDestination(fileName,
                                                                                               client,
                                                                                               downloadUrl,
                                                                                               destinationDirectoryName);

                                                         return GetGcloudExecutablePath(destinationDirectoryName);
                                                     });
            }

            InstallGkeAuthPlugin();
        }

        void InstallGkeAuthPlugin()
        {
            if (!CalamariEnvironment.IsRunningOnWindows)
                return;

            log("Checking if GKE GCloud Auth Plugin needs installation");
            
            var variables = new Dictionary<string, string>();
            var pythonCopyPath = ExecuteCommandAndReturnResult(GcloudExecutable, "components copy-bundled-python", ".", variables);
            variables.Add("CLOUDSDK_PYTHON", pythonCopyPath);

            var gkeComponent = ExecuteCommandAndReturnResult($"\"{GcloudExecutable}\"", "components list --filter=\"Name=gke-gcloud-auth-plugin\" --format=\"json\"", ".", variables);
            var gkeComponentJObject = JArray.Parse(gkeComponent).First();
            var installedState = gkeComponentJObject["state"]["name"].Value<string>();
            
            log($"GKE GCloud Auth Plugin is {installedState}");
            if (installedState != "Installed")
            {
                log("Installing GKE GCloud Auth Plugin");
                ExecuteCommandAndReturnResult(GcloudExecutable, "components install gke-gcloud-auth-plugin --quiet", ".", variables);
                
                log($"Installed GKE GCloud Auth Plugin to {GkeGCloudAuthPluginExecutable}");
            }
            
            // the auth plugin is installed to the same folder as the main gcloud executable (as this is windows we know its always an exe)
            GkeGCloudAuthPluginExecutable = Path.Combine(Path.GetDirectoryName(GcloudExecutable), "gke-gcloud-auth-plugin.exe");
        }

        public async Task InstallKubelogin()
        {
            using (var client = new HttpClient())
            {
                KubeloginExecutable = await DownloadCli("kubelogin",
                                                        () => Task.FromResult<(string, string)>(("v0.0.25", string.Empty)),
                                                        async (destinationDirectoryName, tuple) =>
                                                        {
                                                            var downloadUrl = GetKubeloginDownloadLink(tuple.version);
                                                            var fileName = GetKubeloginZipFileName();

                                                            await DownloadAndExtractToDestination(fileName,
                                                                                                  client,
                                                                                                  downloadUrl,
                                                                                                  destinationDirectoryName);

                                                            return GetKubeloginExecutablePath(destinationDirectoryName);
                                                        });
            }
        }

        static string ExecuteCommandAndReturnResult(string executable, string arguments, string workingDirectory, Dictionary<string, string> environmentVariables = null)
        {
            var stdOut = new StringBuilder();
            var stdError = new StringBuilder();
            var commandExitCode = SilentProcessRunner.ExecuteCommand(executable,
                                                                     arguments,
                                                                     workingDirectory,
                                                                     environmentVariables ?? new Dictionary<string, string>(),
                                                                     (Action<string>)(s => stdOut.AppendLine(s)),
                                                                     (Action<string>)(s => stdError.AppendLine(s)))
                                                     .ExitCode;

            if (commandExitCode != 0)
            {
                throw new InvalidOperationException($"stdOut: {stdOut}, stdError: {stdError}");
            }

            return stdOut.ToString().Trim('\r', '\n');
        }

        static void AddExecutePermission(string exePath)
        {
            if (CalamariEnvironment.IsRunningOnWindows)
                return;

            ExecuteCommandAndReturnResult("chmod",
                                          $"+x {exePath}",
                                          Path.GetDirectoryName(exePath) ?? string.Empty);
        }

        static string GetTerraformFileName(string currentVersion)
        {
            if (CalamariEnvironment.IsRunningOnNix)
                return $"terraform_{currentVersion}_linux_amd64.zip";
            if (CalamariEnvironment.IsRunningOnMac)
                return $"terraform_{currentVersion}_darwin_amd64.zip";

            return $"terraform_{currentVersion}_windows_amd64.zip";
        }

        static string GetAWSAuthenticatorFileNameEndsWith()
        {
            if (CalamariEnvironment.IsRunningOnNix)
                return "_linux_amd64";
            if (CalamariEnvironment.IsRunningOnMac)
                return "_darwin_amd64";

            return "_windows_amd64.exe";
        }

        static string GetKubectlFileName()
        {
            if (CalamariEnvironment.IsRunningOnWindows)
                return "kubectl.exe";

            return "kubectl";
        }

        static string GetAWSAuthenticatorFileName()
        {
            if (CalamariEnvironment.IsRunningOnWindows)
                return "aws-iam-authenticator.exe";

            return "aws-iam-authenticator";
        }

        static string GetAWSCliFileName()
        {
            if (CalamariEnvironment.IsRunningOnNix)
                return "awscli-exe-linux-x86_64.zip";
            if (CalamariEnvironment.IsRunningOnMac)
                return "AWSCLIV2.pkg";

            return "AWSCLIV2.msi";
        }

        static string GetAwsCliDownloadLink(string version)
        {
            var versionString = version != "latest" ? $"-{version}" : "";
            if (CalamariEnvironment.IsRunningOnNix)
                return $"https://awscli.amazonaws.com/awscli-exe-linux-x86_64{versionString}.zip";
            if (CalamariEnvironment.IsRunningOnMac)
                return $"https://awscli.amazonaws.com/AWSCLIV2{versionString}.pkg";

            return $"https://awscli.amazonaws.com/AWSCLIV2{versionString}.msi";
        }

        static string GetAwsCliExecutablePath(string extractPath)
        {
            if (CalamariEnvironment.IsRunningOnWindows)
            {
                return Path.Combine(extractPath,
                                    "extract",
                                    "Amazon",
                                    "AWSCLIV2",
                                    "aws.exe");
            }

            // For developers on a Mac - ensure aws cli is installed manually and on your PATH.
            if (CalamariEnvironment.IsRunningOnMac)
            {
                return "aws";
            }

            return Path.Combine(extractPath, "aws", "dist", "aws");
        }

        static string GetKubectlDownloadLink(string currentVersion)
        {
            if (CalamariEnvironment.IsRunningOnNix)
                return $"https://dl.k8s.io/release/{currentVersion}/bin/linux/amd64/kubectl";
            if (CalamariEnvironment.IsRunningOnMac)
                return $"https://dl.k8s.io/release/{currentVersion}/bin/darwin/amd64/kubectl";

            return $"https://dl.k8s.io/release/{currentVersion}/bin/windows/amd64/kubectl.exe";
        }

        static string GetGcloudDownloadLink(string currentVersion)
        {
            return $"https://dl.google.com/dl/cloudsdk/channels/rapid/downloads/{GetGcloudZipFileName(currentVersion)}";
        }

        static string GetKubeloginDownloadLink(string currentVersion)
        {
            if (CalamariEnvironment.IsRunningOnNix)
            {
                return $"https://github.com/Azure/kubelogin/releases/download/{currentVersion}/kubelogin-linux-amd64.zip";
            }

            return $"https://github.com/Azure/kubelogin/releases/download/{currentVersion}/kubelogin-win-amd64.zip";
        }

        public string GetKubeloginZipFileName()
        {
            if (CalamariEnvironment.IsRunningOnWindows)
            {
                return $"kubelogin.zip";
            }

            return $"kubelogin-linux-amd64.zip";
        }

        static string GetKubeloginExecutablePath(string extractPath)
        {
            var executableName = CalamariEnvironment.IsRunningOnWindows ? "kubelogin.exe" : "kubelogin";
            return Path.Combine(extractPath, "bin", CalamariEnvironment.IsRunningOnWindows ? "windows_amd64" : "linux_amd64", executableName);
        }

        static string GetGcloudZipFileName(string currentVersion)
        {
            if (CalamariEnvironment.IsRunningOnNix)
                return $"google-cloud-sdk-{currentVersion}-linux-x86_64.tar.gz";
            if (CalamariEnvironment.IsRunningOnMac)
                return $"google-cloud-sdk-{currentVersion}-darwin-x86_64-bundled-python.tar.gz";

            return $"google-cloud-sdk-{currentVersion}-windows-x86_64-bundled-python.zip";
        }

        static string GetGcloudExecutablePath(string extractPath)
        {
            var executableName = string.Empty;
            if (CalamariEnvironment.IsRunningOnWindows)
                executableName = "gcloud.cmd";
            else
                executableName = "gcloud";

            return Path.Combine(extractPath, "google-cloud-sdk", "bin", executableName);
        }

        static async Task Download(string path,
                                   HttpClient client,
                                   string downloadUrl)
        {
            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var stream = await client.GetStreamAsync(downloadUrl))
            {
                await stream.CopyToAsync(fileStream);
            }
        }

        static async Task DownloadTerraform(string fileName,
                                            HttpClient client,
                                            string downloadBaseUrl,
                                            string destination)
        {
            var zipPath = Path.Combine(Path.GetTempPath(), fileName);
            var downloadUrl = UriCombine(downloadBaseUrl, fileName);
            using (new TemporaryFile(zipPath))
            {
                await Download(zipPath, client, downloadUrl);

                ZipFile.ExtractToDirectory(zipPath, destination);
            }
        }

        static string UriCombine(string downloadBaseUrl, string fileName)
        {
            if (downloadBaseUrl.Last() != '/')
                downloadBaseUrl += '/';

            return $"{downloadBaseUrl}{fileName}";
        }

        static async Task DownloadAndExtractToDestination(string fileName,
                                                          HttpClient client,
                                                          string downloadUrl,
                                                          string destination)
        {
            var zipPath = Path.Combine(Path.GetTempPath(), fileName);
            using (new TemporaryFile(zipPath))
            {
                await Download(zipPath, client, downloadUrl);
                using (Stream stream = File.OpenRead(zipPath))
                using (var reader = ReaderFactory.Open(stream))
                {
                    reader.WriteAllToDirectory(destination, new ExtractionOptions { ExtractFullPath = true, Overwrite = true, WriteSymbolicLink = WarnThatSymbolicLinksAreNotSupported });
                }
            }
        }

        static void WarnThatSymbolicLinksAreNotSupported(string sourcepath, string targetpath)
        {
            TestContext.Progress.WriteLine("Cannot create symbolic link: {0}, Calamari does not currently support the extraction of symbolic links", sourcepath);
        }

        async Task<string> DownloadCli(string toolName, Func<Task<(string version, string data)>> versionFetcher, Func<string, (string version, string data), Task<string>> downloader)
        {
            var data = await versionFetcher();
            var destinationDirectoryName = TestEnvironment.GetTestPath("Tools", toolName, data.version);

            string ShouldDownload()
            {
                if (!Directory.Exists(destinationDirectoryName))
                {
                    return null;
                }

                var path = Directory.EnumerateFiles(destinationDirectoryName).FirstOrDefault();
                if (toolName == "gcloud")
                {
                    path = GetGcloudExecutablePath(destinationDirectoryName);
                }

                if (toolName == "aws")
                {
                    path = GetAwsCliExecutablePath(destinationDirectoryName);
                }

                if (toolName == "kubelogin")
                {
                    path = GetKubeloginExecutablePath(destinationDirectoryName);
                }

                if (path == null || !File.Exists(path))
                {
                    return null;
                }

                log($"Using existing {toolName} located in {path}");
                return path;
            }

            var executablePath = ShouldDownload();
            if (!String.IsNullOrEmpty(executablePath))
            {
                return executablePath;
            }

            log($"Downloading {toolName} cli...");
            Directory.CreateDirectory(destinationDirectoryName);

            var retry = new RetryTracker(3, TimeSpan.MaxValue, new LimitedExponentialRetryInterval(1000, 30000, 2));
            while (retry.Try())
            {
                try
                {
                    executablePath = await downloader(destinationDirectoryName, data);

                    AddExecutePermission(executablePath);
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

            log($"Downloaded {toolName} to {executablePath}");

            return executablePath;
        }
    }
}