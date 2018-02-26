using System;
using System.Net;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    /// <summary>
    /// This class knows how to interpret a package id and request a download
    /// from a specific downloader implementation. 
    /// </summary>
    public class PackageDownloaderStrategy
    {
        public static PackagePhysicalFileMetadata DownloadPackage(
            string packageId,
            IVersion version,
            string feedId,
            Uri feedUri,
            FeedType feedType,
            ICredentials feedCredentials,
            bool forcePackageDownload,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            IPackageDownloader downloader = null;
            switch (feedType)
            {
                case FeedType.Maven:
                    downloader = new MavenPackageDownloader();
                    break;
                case FeedType.NuGet:
                    downloader = new NuGetPackageDownloader();
                    break;
                case FeedType.GitHub:
                    downloader = new GitHubPackageDownloader();
                    break;
                default:
                    throw new NotImplementedException($"No Calamari downloader exists for feed type `{feedType}`.");
            }
            Log.Verbose($"Feed type provided `{feedType}` using {downloader.GetType().Name}");

            return downloader.DownloadPackage(
                packageId,
                version, 
                feedId, 
                feedUri, 
                feedCredentials, 
                forcePackageDownload, 
                maxDownloadAttempts, 
                downloadAttemptBackoff);
        }
    }
}