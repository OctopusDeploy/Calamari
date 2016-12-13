using System;
using System.Net;
using System.Threading;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Retry;
#if USE_NUGET_V2_LIBS
using Calamari.NuGet.Versioning;
#else
using NuGet.Versioning;
#endif

namespace Calamari.Integration.Packages.NuGet
{
    internal class NuGetPackageDownloader
    {
        private readonly RetryTracker retry;
        private readonly CalamariPhysicalFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        internal const int NumberOfTimesToRetryOnFailure = 4;
        internal const int NumberOfTimesToAttemptToDownloadPackage = NumberOfTimesToRetryOnFailure + 1;

        public NuGetPackageDownloader() : this(GetRetryTracker())
        {
        }

        public NuGetPackageDownloader(RetryTracker retry)
        {
            this.retry = retry;
        }

        public void DownloadPackage(string packageId, NuGetVersion version, Uri feedUri, ICredentials feedCredentials, string targetFilePath)
        {
            DownloadPackage(packageId, version, feedUri, feedCredentials, targetFilePath, DownloadPackageAction);
        }

        public void DownloadPackage(string packageId, NuGetVersion version, Uri feedUri, ICredentials feedCredentials, string targetFilePath, Action<string, NuGetVersion, Uri, ICredentials, string> action)
        {
            while (retry.Try())
            {
                Log.Verbose($"Downloading package (attempt {retry.CurrentTry} of {NumberOfTimesToAttemptToDownloadPackage})");

                try
                {
                    action(packageId, version, feedUri, feedCredentials, targetFilePath);
                    return;
                }
                catch (Exception ex)
                {
                    Log.Verbose($"Attempt {retry.CurrentTry} of {NumberOfTimesToAttemptToDownloadPackage}: {ex.Message}");

                    fileSystem.DeleteFile(targetFilePath, FailureOptions.IgnoreFailure);

                    if (retry.CanRetry())
                    {
                        var wait = TimeSpan.FromMilliseconds(retry.Sleep());
                        Log.Verbose($"Going to wait {wait.TotalSeconds}s before attempting the download from the external feed again.");
                        Thread.Sleep(wait);
                    }
                    else
                    {
                        var helpfulFailure = $"The package {packageId} version {version} could not be downloaded from the external feed '{feedUri}' after making {NumberOfTimesToAttemptToDownloadPackage} attempts over a total of {Math.Floor(retry.TotalElapsed.TotalSeconds)}s. Make sure the package is pushed to the external feed and try the deployment again. If this is part of an automated deployment, make sure all packages are pushed to the external feed before starting the deployment. If the packages are pushed, perhaps the external feed hasn't finished updating its index and you need to give the external feed more time to update its index before starting the deployment. If you are getting a package verification error, try switching to a Windows File Share package repository to see if that helps.";
                        helpfulFailure += $"{Environment.NewLine}{ex}";

                        throw new Exception(helpfulFailure, ex);
                    }
                }
            }
        }

        private void DownloadPackageAction(string packageId, NuGetVersion version, Uri feedUri, ICredentials feedCredentials, string targetFilePath)
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

        static RetryTracker GetRetryTracker()
        {
            return new RetryTracker(maxRetries: NumberOfTimesToRetryOnFailure, timeLimit: null, retryInterval: new RetryInterval(5000, 100000, 2));
        }
    }
}
