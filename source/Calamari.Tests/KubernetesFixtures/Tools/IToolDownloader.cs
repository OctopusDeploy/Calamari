#if NETCORE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Tests.KubernetesFixtures.Tools;
using Octopus.CoreUtilities.Extensions;
using Serilog;

namespace Calamari.Tests.KubernetesFixtures.Tools
{
    public interface IToolDownloader
    {
        Task<string> Download(string targetDirectory, CancellationToken cancellationToken);
    }


    /// <summary>
    /// Copied as is from the octopus server repo.
    /// </summary>
    public static class OctopusPackageDownloader
    {
        public static async Task DownloadPackage(string downloadUrl, string filePath, ILogger logger, CancellationToken cancellationToken = default)
        {
            var exceptions = new List<Exception>();
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    await AttemptToDownloadPackage(downloadUrl, filePath, logger, cancellationToken);
                    return;
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            throw new AggregateException(exceptions);
        }

        static async Task AttemptToDownloadPackage(string downloadUrl, string filePath, ILogger logger, CancellationToken cancellationToken)
        {
            var totalTime = Stopwatch.StartNew();
            var totalRead = 0L;
            string expectedHash = null;
            try
            {
                using (var client = new HttpClient())
                {
                    // This appears to be the time it takes to do a single read/write, not the entire download.
                    client.Timeout = TimeSpan.FromSeconds(20);
                    using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();
                        var totalLength = response.Content.Headers.ContentLength;
                        expectedHash = TryGetExpectedHashFromHeaders(response, expectedHash);

                        logger.Information($"Downloading {downloadUrl} ({totalLength} bytes)");

                        var sw = new Stopwatch();
                        sw.Start();
                        using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
                                      fileStream = new FileStream(
                                                                  filePath,
                                                                  FileMode.Create,
                                                                  FileAccess.Write,
                                                                  FileShare.None,
                                                                  8192,
                                                                  true))
                        {

                            var buffer = new byte[8192];

                            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                            while (read != 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, read, cancellationToken);

                                if (totalLength.HasValue && sw.ElapsedMilliseconds >= TimeSpan.FromSeconds(7).TotalMilliseconds)
                                {
                                    var percentRead = totalRead * 1.0 / totalLength.Value * 100;
                                    logger.Information($"Downloading Completed {percentRead}%");
                                    sw.Reset();
                                    sw.Start();
                                }

                                read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                                totalRead += read;
                            }

                            totalTime.Stop();

                            logger.Information("Download Finished in {totalTime}ms", totalTime.ElapsedMilliseconds);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failure to download: {downloadUrl}. After {totalTime.Elapsed.TotalSeconds} seconds we only downloaded, {totalRead}", e);
            }

            ValidateDownload(filePath, expectedHash);
        }

        static string TryGetExpectedHashFromHeaders(HttpResponseMessage response, string expectedHash)
        {
            if (response.Headers.TryGetValues("x-amz-meta-sha256", out var expectedHashs))
            {
                expectedHash = expectedHashs.FirstOrDefault();
            }

            return expectedHash;
        }

        static void ValidateDownload(string filePath, string expectedHash)
        {
            if (!expectedHash.IsNullOrEmpty())
            {
                using (var sha256 = SHA256.Create())
                {
                    var fileBytes = File.ReadAllBytes(filePath);
                    var hash = sha256.ComputeHash(fileBytes);
                    var computedHash = BitConverter.ToString(hash).Replace("-", "");
                    if (!computedHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception($"Computed SHA256 ({computedHash}) hash of file does not match expected ({expectedHash})." + $"Downloaded file may be corrupt. File size {((long)fileBytes.Length)}");
                    }
                }
            }
        }
    }

    public abstract class ToolDownloader : IToolDownloader
    {
        readonly OperatingSystem os;

        protected ILogger Logger { get; }
        protected string ExecutableName { get; }

        protected ToolDownloader(string executableName, ILogger logger)
        {
            ExecutableName = executableName;
            Logger = logger;

            os = GetOperationSystem();

            //we assume that windows always has .exe suffixed
            if (os is OperatingSystem.Windows)
            {
                ExecutableName += ".exe";
            }
        }

        public async Task<string> Download(string targetDirectory, CancellationToken cancellationToken)
        {
            var downloadUrl = BuildDownloadUrl(RuntimeInformation.ProcessArchitecture, os);

            //we download to a random file name
            var downloadFilePath = Path.Combine(targetDirectory, Guid.NewGuid().ToString("N"));

            Logger.Information("Downloading {DownloadUrl} to {DownloadFilePath}", downloadUrl, downloadFilePath);
            await OctopusPackageDownloader.DownloadPackage(downloadUrl, downloadFilePath, Logger, cancellationToken);

            downloadFilePath = PostDownload(targetDirectory, downloadFilePath, RuntimeInformation.ProcessArchitecture, os);

            //if this is not running on windows, chmod the tool to be executable
            if (os != OperatingSystem.Windows)
            {
                var exitCode = SilentProcessRunner.ExecuteCommand(
                                                                  "chmod",
                                                                  $"+x {downloadFilePath}",
                                                                  targetDirectory,
                                                                  new Dictionary<string, string>(),
                                                                  (x) => Logger.Information(x),
                                                                  (m) => Logger.Error(m));

                if (exitCode.ExitCode != 0)
                {
                    Logger.Error("Error running chmod against executable {ExecutablePath}", downloadFilePath);
                }
            }

            return downloadFilePath;
        }

        protected abstract string BuildDownloadUrl(Architecture processArchitecture, OperatingSystem operatingSystem);

        protected virtual string PostDownload(string downloadDirectory, string downloadFilePath, Architecture processArchitecture, OperatingSystem operatingSystem)
        {
            var targetFilename = Path.Combine(downloadDirectory, ExecutableName);
            File.Move(downloadFilePath, targetFilename);

            return targetFilename;
        }

        static OperatingSystem GetOperationSystem()
        {
            if (PlatformDetection.IsRunningOnWindows)
            {
                return OperatingSystem.Windows;
            }

            if (PlatformDetection.IsRunningOnNix)
            {
                return OperatingSystem.Nix;
            }

            if (PlatformDetection.IsRunningOnMac)
            {
                return OperatingSystem.Mac;
            }

            throw new InvalidOperationException("Unsupported OS");
        }
    }

    public static class PlatformDetection
    {
        public static bool IsRunningOnNix => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static bool IsRunningOnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsRunningOnMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }

    public enum OperatingSystem
    {
        Windows,
        Nix,
        Mac
    }

    public class RequiredToolDownloader
    {
        readonly TemporaryDirectory temporaryDirectory;

        readonly KindDownloader kindDownloader;
        //  readonly HelmDownloader helmDownloader;
         readonly KubeCtlDownloader kubeCtlDownloader;

        public RequiredToolDownloader(TemporaryDirectory temporaryDirectory, ILogger logger)
        {
            this.temporaryDirectory = temporaryDirectory;

            kindDownloader = new KindDownloader(logger);
            //helmDownloader = new HelmDownloader(logger);
            kubeCtlDownloader = new KubeCtlDownloader(logger);
        }

        public async Task<(string KindExePath, string HelmExePath, string KubeCtlPath)> DownloadRequiredTools(CancellationToken cancellationToken)
        {
            var kindExePathTask = kindDownloader.Download(temporaryDirectory.DirectoryPath, cancellationToken);
            var helmExePathTask = kindExePathTask; //helmDownloader.Download(temporaryDirectory.DirectoryPath, cancellationToken);
            var kubeCtlExePathTask = kubeCtlDownloader.Download(temporaryDirectory.DirectoryPath, cancellationToken);

            await Task.WhenAll(kindExePathTask, helmExePathTask, kubeCtlExePathTask);

            return (kindExePathTask.Result, helmExePathTask.Result, kubeCtlExePathTask.Result);
        }
    }
}
#endif