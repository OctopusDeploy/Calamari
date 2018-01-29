using System;
using System.Net;
using System.Threading;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Download;
using Calamari.Integration.Retry;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.NuGet
{
    internal class NuGetPackageDownloader
    {
        private readonly ICalamariFileSystem fileSystem;

        public NuGetPackageDownloader(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void DownloadPackage(
            string packageId, 
            IVersion version, 
            Uri feedUri, 
            ICredentials feedCredentials, 
            string targetFilePath, 
            int maxDownloadAttempts, 
            TimeSpan downloadAttemptBackoff)
        {
            DownloadPackage(
                packageId, 
                version, 
                feedUri, 
                feedCredentials, 
                targetFilePath,
                maxDownloadAttempts, 
                downloadAttemptBackoff,
                DownloadPackageAction);
        }

        internal void DownloadPackage(
            string packageId, 
            IVersion version, 
            Uri feedUri, 
            ICredentials feedCredentials,
            string targetFilePath, 
            int maxDownloadAttempts, 
            TimeSpan downloadAttemptBackoff, 
            Action<string, IVersion, Uri, ICredentials, string> action)
        {
            if (maxDownloadAttempts <= 0)
                throw new ArgumentException($"The number of download attempts should be greater than zero, but was {maxDownloadAttempts}", nameof(maxDownloadAttempts));

            var tempTargetFilePath = targetFilePath + Download.NuGetPackageDownloader.DownloadingExtension;

            // The RetryTracker is a bit finicky to set up...
            var numberOfRetriesOnFailure = maxDownloadAttempts-1;
            var retry = new RetryTracker(numberOfRetriesOnFailure, timeLimit: null, retryInterval: new LinearRetryInterval(downloadAttemptBackoff));
            while (retry.Try())
            {
                Log.Verbose($"Downloading package (attempt {retry.CurrentTry} of {maxDownloadAttempts})");

                try
                {
                    action(packageId, version, feedUri, feedCredentials, tempTargetFilePath);
                    fileSystem.MoveFile(tempTargetFilePath, targetFilePath);
                    return;
                }
                catch (Exception ex)
                {
                    Log.Verbose($"Attempt {retry.CurrentTry} of {maxDownloadAttempts}: {ex.Message}");

                    fileSystem.DeleteFile(tempTargetFilePath, FailureOptions.IgnoreFailure);
                    fileSystem.DeleteFile(targetFilePath, FailureOptions.IgnoreFailure);

                    if (retry.CanRetry())
                    {
                        var wait = TimeSpan.FromMilliseconds(retry.Sleep());
                        Log.Verbose($"Going to wait {wait.TotalSeconds}s before attempting the download from the external feed again.");
                        Thread.Sleep(wait);
                    }
                    else
                    {
                        var helpfulFailure = $"The package {packageId} version {version} could not be downloaded from the external feed '{feedUri}' after making {maxDownloadAttempts} attempts over a total of {Math.Floor(retry.TotalElapsed.TotalSeconds)}s. Make sure the package is pushed to the external feed and try the deployment again. For a detailed troubleshooting guide go to http://g.octopushq.com/TroubleshootMissingPackages";
                        helpfulFailure += $"{Environment.NewLine}{ex}";

                        throw new Exception(helpfulFailure, ex);
                    }
                }
            }
        }

        private void DownloadPackageAction(string packageId, IVersion version, Uri feedUri, ICredentials feedCredentials, string targetFilePath)
        {
            // FileSystem feed 
            if (feedUri.IsFile)
            {
                NuGetFileSystemDownloader.DownloadPackage(packageId, version, feedUri, targetFilePath);
            }

#if USE_NUGET_V2_LIBS
            // NuGet V3 feed 
            else if (IsHttp(feedUri.ToString()) && feedUri.ToString().EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                NuGetV3Downloader.DownloadPackage(packageId, version, feedUri, feedCredentials, targetFilePath);
            }

            // V2 feed
            else 
            {
                NuGetV2Downloader.DownloadPackage(packageId, version.ToString(), feedUri, feedCredentials, targetFilePath);
            }
#else
            else
            {
                NuGetV3LibDownloader.DownloadPackage(packageId, version, feedUri, feedCredentials, targetFilePath);
            }
#endif
        }

        bool IsHttp(string uri)
        {
            return uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }
    }
}
