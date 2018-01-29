using System;
using System.Net;
using Octopus.Versioning;
using Octopus.Versioning.Metadata;

namespace Calamari.Integration.Packages.Download
{
    /// <summary>
    /// This class knows how to interpret a package id and request a download
    /// from a specific downloader implementation. 
    /// </summary>
    public class PackageDownloaderStrategy : IPackageDownloader
    {
        static readonly IPackageIDParser MavenPackageIdParser = new MavenPackageIDParser();
        static readonly IPackageIDParser NugetPackageIdParser = new NuGetPackageIDParser();

        public void DownloadPackage(
            string packageId,
            IVersion version,
            string feedId,
            Uri feedUri,
            ICredentials feedCredentials,
            bool forcePackageDownload,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff,
            out string downloadedTo,
            out string hash, 
            out long size)
        {
            IPackageDownloader downloader = null;
            if (MavenPackageIdParser.TryGetMetadataFromPackageID(packageId, out var mavenMetadata))
            {
                downloader = new MavenPackageDownloader();
            }
            else if (NugetPackageIdParser.TryGetMetadataFromPackageID(packageId, out var nugetMetadata))
            {
                downloader = new NuGetPackageDownloader();                
            }
            else
            {
                throw new NotImplementedException($"Package ID {packageId} is not recognised.");
            }
            
            downloader.DownloadPackage(
                packageId,
                version, 
                feedId, 
                feedUri, 
                feedCredentials, 
                forcePackageDownload, 
                maxDownloadAttempts, 
                downloadAttemptBackoff, 
                out downloadedTo, 
                out hash, 
                out size);
        }
    }
}