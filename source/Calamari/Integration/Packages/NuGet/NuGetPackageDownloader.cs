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
                    Log.VerboseFormat("Attempt {0} of {1}: Unable to download package: {2}", retry.CurrentTry,
                        NumberOfTimesToAttemptToDownloadPackage, ex.ToString());

                    fileSystem.DeleteFile(targetFilePath, FailureOptions.IgnoreFailure);

                    if (retry.CanRetry())
                    {
                        Thread.Sleep(retry.Sleep());
                    }
                    else
                    {
                        Log.ErrorFormat("Unable to download package: {0}", ex.Message);
                        throw new Exception(
                            "The package could not be downloaded from NuGet. If you are getting a package verification error, try switching to a Windows File Share package repository to see if that helps.");
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
            return new RetryTracker(maxRetries: NumberOfTimesToRetryOnFailure, timeLimit: null, retryInterval: new RetryInterval(1000, 15000, 2));
        }
    }
}
