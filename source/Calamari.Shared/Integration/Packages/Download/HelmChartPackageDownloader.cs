using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Util;
using Octopus.Versioning;
#if SUPPORTS_POLLY
using Polly;
#endif

namespace Calamari.Integration.Packages.Download
{
    public class HelmChartPackageDownloader: IPackageDownloader
    {
        private static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();
        const string Extension = ".tgz";
        private readonly ICalamariFileSystem fileSystem;

        public HelmChartPackageDownloader(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }
        
        public PackagePhysicalFileMetadata DownloadPackage(string packageId, IVersion version, string feedId, Uri feedUri,
            ICredentials feedCredentials, bool forcePackageDownload, int maxDownloadAttempts, TimeSpan downloadAttemptBackoff)
        {
            var cacheDirectory = PackageDownloaderUtils.GetPackageRoot(feedId);
            fileSystem.EnsureDirectoryExists(cacheDirectory);
            
            if (!forcePackageDownload)
            {
                var downloaded = SourceFromCache(packageId, version, cacheDirectory);
                if (downloaded != null)
                {
                    Log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloaded.FullFilePath);
                    return downloaded;
                }
            }
            
            return DownloadChart(packageId, version, feedUri, feedCredentials, cacheDirectory);
        }
        
        const string TempRepoName = "octopusfeed";

        PackagePhysicalFileMetadata DownloadChart(string packageId, IVersion version, Uri feedUri,
            ICredentials feedCredentials, string cacheDirectory)
        {
            var cred = feedCredentials.GetCredential(feedUri, "basic");

            var tempDirectory = fileSystem.CreateTemporaryDirectory();

            using (new TemporaryDirectory(tempDirectory))
            {
                
                var homeDir = Path.Combine(tempDirectory, "helm");
                if (!Directory.Exists(homeDir))
                {
                    Directory.CreateDirectory(homeDir);
                }
                var stagingDir = Path.Combine(tempDirectory, "staging");
                if (!Directory.Exists(stagingDir))
                {
                    Directory.CreateDirectory(stagingDir);
                }

                var log = new LogWrapper();

                var localDownloadName =
                    Path.Combine(cacheDirectory, PackageName.ToCachedFileName(packageId, version, Extension));

                fileSystem.MoveFile(Directory.GetFiles(stagingDir)[0], localDownloadName);
                return PackagePhysicalFileMetadata.Build(localDownloadName);
            }
        }

        void RunCommandsForHelm2(string url, string packageId, IVersion version, string homeDir, string stagingDir, string tempDirectory, NetworkCredential cred, ILog log)
        {
            InvokeWithRetry(() => Invoke($"init --home \"{homeDir}\" --client-only --debug", tempDirectory, log, "initialise"));
            InvokeWithRetry(() => Invoke($"repo add --home \"{homeDir}\" {(string.IsNullOrEmpty(cred.UserName) ? "" : $"--username \"{cred.UserName}\" --password \"{cred.Password}\"")} --debug {TempRepoName} {url}", tempDirectory, log, "add the chart repository"));
            InvokeWithRetry(() => Invoke($"fetch --home \"{homeDir}\"  --version \"{version}\" --destination \"{stagingDir}\" --debug {TempRepoName}/{packageId}", tempDirectory, log, "download the chart"));
        }

        void RunCommandsForHelm3(string url, string packageId, IVersion version, string stagingDir, string directory, NetworkCredential cred, ILog log)
        {
            InvokeWithRetry(() =>
                Invoke(
                    $"repo add {(string.IsNullOrEmpty(cred.UserName) ? "" : $"--username \"{cred.UserName}\" --password \"{cred.Password}\"")} {TempRepoName} {url}",
                    directory, log, "add the chart repository"));
            InvokeWithRetry(() =>
                Invoke($"pull --version \"{version}\" --destination \"{stagingDir}\" {TempRepoName}/{packageId}",
                    directory, log, "download the chart"));
        }
        
#if SUPPORTS_POLLY
        void InvokeWithRetry(Action action)
        {
            Policy.Handle<Exception>()
                .WaitAndRetry(4, retry => TimeSpan.FromSeconds(retry), (ex, timespan) =>
                {
                    Console.WriteLine($"Command failed. Retrying in {timespan}.");
                })
                .Execute(action);
        }
#else
        //net40 doesn't support polly... usage is low enough to skip the effort to implement nice retries
        void InvokeWithRetry(Action action) => action();
#endif
        
        public void Invoke(string args, string dir, ILog log, string specificAction)
        {
            InvokeWithOutput(args, dir, log, specificAction);
        }
        
        string InvokeWithOutput(string args, string dir, ILog log, string specificAction)
        {
            var info = new ProcessStartInfo("helm", args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = dir,
                CreateNoWindow = true
            };

            var sw = Stopwatch.StartNew();
            var timeout = TimeSpan.FromSeconds(30);
            using (var server = Process.Start(info))
            {
                while (!server.WaitForExit(10000) && sw.Elapsed < timeout)
                {
                    log.Warn($"Still waiting for {info.FileName} {info.Arguments} [PID:{server.Id}] to exit after waiting {sw.Elapsed}...");
                }

                var stdout = server.StandardOutput.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    log.Verbose(stdout);
                }

                var stderr = server.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    log.Error(stderr);
                }

                if (!server.HasExited)
                {
                    server.Kill();
                    throw new CommandException($"Helm failed to {specificAction} in an appropriate period of time ({timeout.TotalSeconds} sec). Please try again or check your connection.");
                }

                if (server.ExitCode != 0)
                {
                    throw new CommandException($"Helm failed to {specificAction} (Exit code {server.ExitCode}). Error output: \r\n{stderr}");
                }

                return stdout;
            }
        }

        PackagePhysicalFileMetadata SourceFromCache(string packageId, IVersion version, string cacheDirectory)
        {
            Log.VerboseFormat("Checking package cache for package {0} v{1}", packageId, version.ToString());

            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version, new [] { Extension }));

            foreach (var file in files)
            {
                var package = PackageName.FromFile(file);
                if (package == null)
                    continue;

                if (string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase) && package.Version.Equals(version))
                {
                    return PackagePhysicalFileMetadata.Build(file, package);
                }
            }

            return null;
        }
    }
}