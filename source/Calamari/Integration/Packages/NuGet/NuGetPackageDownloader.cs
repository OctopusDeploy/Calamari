using System;
using System.Net;
using System.Threading;
using Calamari.Integration.Retry;
using NuGet.Versioning;

namespace Calamari.Integration.Packages.NuGet
{
    internal class NuGetPackageDownloader
    {
        const int NumberOfTimesToAttemptToDownloadPackage = 5;

        public static void DownloadPackage(string packageId, NuGetVersion version, Uri feedUri, ICredentials feedCredentials, string targetFilePath)
        {
            var retry = GetRetryTracker();

            while (retry.Try())
            {
                Log.Verbose($"Downloading package (attempt {retry.CurrentTry} of {NumberOfTimesToAttemptToDownloadPackage})");

                try
                {
                    // FileSystem feed 
                    if (feedUri.IsFile)
                    {
                        NuGetFileSystemDownloader.DownloadPackage(packageId, version, feedUri, targetFilePath);
                    }

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

                    return;
                }
                catch (Exception ex)
                {
                    if (retry.CanRetry())
                    {
                        Log.VerboseFormat("Attempt {0} of {1}: Unable to download package: {2}", retry.CurrentTry,
                            NumberOfTimesToAttemptToDownloadPackage, ex.ToString());

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

        static bool IsHttp(string uri)
        {
            return uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        static RetryTracker GetRetryTracker()
        {
            return new RetryTracker(maxRetries: NumberOfTimesToAttemptToDownloadPackage, timeLimit: null, retryInterval: new RetryInterval(1000, 15000, 2));
        }
    }
}