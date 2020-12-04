using System;
using System.Net;
using System.Threading;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Retry;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Download;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.NuGet
{
    public class InternalNuGetPackageDownloader
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IVariables variables;

        public InternalNuGetPackageDownloader(ICalamariFileSystem fileSystem, IVariables variables)
        {
            this.fileSystem = fileSystem;
            this.variables = variables;
        }

        public void DownloadPackage(string packageId, IVersion version, Uri feedUri, ICredentials feedCredentials, string targetFilePath, int maxDownloadAttempts, TimeSpan downloadAttemptBackoff)
        {
            DownloadPackage(packageId, version, feedUri, feedCredentials, targetFilePath, maxDownloadAttempts, downloadAttemptBackoff, DownloadPackageAction);
        }

        public void DownloadPackage(
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

            var tempTargetFilePath = targetFilePath + NuGetPackageDownloader.DownloadingExtension;

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
                    if (ex is WebException webException &&
                        webException.Response is HttpWebResponse response &&
                        response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new Exception($"Unable to download package: {webException.Message}", ex);
                    }
                    Log.Verbose($"Attempt {retry.CurrentTry} of {maxDownloadAttempts}: {ex.Message}");

                    fileSystem.DeleteFile(tempTargetFilePath, FailureOptions.IgnoreFailure);
                    fileSystem.DeleteFile(targetFilePath, FailureOptions.IgnoreFailure);

                    if (retry.CanRetry())
                    {
                        var wait = retry.Sleep();
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
                var timeout = GetHttpTimeout();
                NuGetV3Downloader.DownloadPackage(packageId, version, feedUri, feedCredentials, targetFilePath, timeout);
            }

            // V2 feed
            else
            {
                WarnIfHttpTimeoutHasBeenSet();
                NuGetV2Downloader.DownloadPackage(packageId, version.ToString(), feedUri, feedCredentials, targetFilePath);
            }
#else
            else
            {
                WarnIfHttpTimeoutHasBeenSet();
                NuGetV3LibDownloader.DownloadPackage(packageId, version, feedUri, feedCredentials, targetFilePath);
            }
#endif
        }

#if USE_NUGET_V2_LIBS
        TimeSpan GetHttpTimeout()
        {
            const string expectedTimespanFormat = "c";
            
            // Equal to Timeout.InfiniteTimeSpan, which isn't available in net40
            var defaultTimeout = new TimeSpan(0, 0, 0, 0, -1);
            
            var rawTimeout = variables.Get(KnownVariables.NugetHttpTimeout);
            if (string.IsNullOrWhiteSpace(rawTimeout))
            {
                return defaultTimeout;
            }

            if (TimeSpan.TryParseExact(rawTimeout, expectedTimespanFormat, null, out var parsedTimeout))
            {
                return parsedTimeout;
            }

            var exampleTimespan = new TimeSpan(0, 0, 1, 0).ToString(expectedTimespanFormat);
            
            var message = $"The variable {KnownVariables.NugetHttpTimeout} couldn't be parsed as a timespan. " +
                          $"Expected a value like '{exampleTimespan}' but received '{rawTimeout}'. " +
                          $"Defaulting to '{defaultTimeout.ToString(expectedTimespanFormat)}'.";

            Log.Warn(message);
            return defaultTimeout;
        }

#endif
        
        void WarnIfHttpTimeoutHasBeenSet()
        {
            if (variables.IsSet(KnownVariables.NugetHttpTimeout))
            {
                Log.Warn(
                    $"A Nuget HTTP timeout was set via the '{KnownVariables.NugetHttpTimeout}' variable. "
                    + "This variable is not supported for this Nuget repository on this version of .NET."
                );
            }
        }

        bool IsHttp(string uri)
        {
            return uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }
    }
}
