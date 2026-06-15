using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Retry;
using Calamari.Testing.Helpers;
using NUnit.Framework;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Calamari.ExternalTools.Tests.Infrastructure
{
    /// <summary>
    /// Downloads and caches external tool binaries.
    /// Cache location: {TestOutputDir}/Tools/{toolName}/{version}/
    /// </summary>
    public class ToolDownloader
    {
        readonly Action<string> log;

        public ToolDownloader(Action<string> log)
        {
            this.log = log;
        }

        public async Task<string> Download(string toolName, string version, Func<string, string, HttpClient, Task<string>> downloadAction)
        {
            var destinationDir = TestEnvironment.GetTestPath("Tools", toolName, version);

            var existing = FindExistingExecutable(toolName, destinationDir);
            if (existing != null)
            {
                log($"Using cached {toolName} {version} at {existing}");
                return existing;
            }

            log($"Downloading {toolName} {version}...");
            Directory.CreateDirectory(destinationDir);

            var retry = new RetryTracker(4, TimeSpan.MaxValue, new LimitedExponentialRetryInterval(3000, 30000, 2));
            string executablePath = null;

            while (retry.Try())
            {
                try
                {
                    using var client = CreateHttpClient();
                    executablePath = await downloadAction(destinationDir, version, client);
                    AddExecutePermission(executablePath);
                    break;
                }
                catch
                {
                    if (!retry.CanRetry())
                        throw;

                    await Task.Delay(retry.Sleep());
                }
            }

            log($"Downloaded {toolName} {version} to {executablePath}");
            return executablePath!;
        }

        string? FindExistingExecutable(string toolName, string destinationDir)
        {
            if (!Directory.Exists(destinationDir))
                return null;

            var path = Directory.EnumerateFiles(destinationDir, "*", SearchOption.AllDirectories)
                .FirstOrDefault(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    return name.Contains(toolName.ToLowerInvariant().Replace("-", ""));
                });

            return path != null && File.Exists(path) ? path : null;
        }

        public static async Task DownloadFile(string url, string destinationPath, HttpClient client)
        {
            using var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);
        }

        public static async Task DownloadAndExtractZip(string url, string destinationDir, HttpClient client)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
            try
            {
                await DownloadFile(url, tempPath, client);
                ZipFile.ExtractToDirectory(tempPath, destinationDir);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        public static async Task DownloadAndExtractTarGz(string url, string destinationDir, HttpClient client)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.tar.gz");
            try
            {
                await DownloadFile(url, tempPath, client);
                using Stream stream = File.OpenRead(tempPath);
                using var reader = ReaderFactory.Open(stream);
                reader.WriteAllToDirectory(destinationDir, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true,
                    WriteSymbolicLink = (source, target) =>
                        TestContext.Progress.WriteLine("Skipping symbolic link: {0}", source)
                });
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        static void AddExecutePermission(string exePath)
        {
            if (CalamariEnvironment.IsRunningOnWindows || string.IsNullOrEmpty(exePath))
                return;

            Calamari.Common.Features.Processes.SilentProcessRunner.ExecuteCommand(
                "chmod", $"+x {exePath}",
                Path.GetDirectoryName(exePath) ?? ".",
                _ => { }, _ => { });
        }

        static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36");
            return client;
        }

        // --- Platform helpers ---

        public static string GetPlatform()
        {
            if (CalamariEnvironment.IsRunningOnWindows) return "windows";
            if (CalamariEnvironment.IsRunningOnMac) return "darwin";
            return "linux";
        }

        public static string GetArchitecture()
        {
            return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture switch
            {
                System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
                _ => "amd64"
            };
        }
    }
}
