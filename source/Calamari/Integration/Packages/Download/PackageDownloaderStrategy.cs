using System;
using System.Net;
using Octopus.Core.Resources;
using Octopus.Core.Resources.Metadata;
using Octopus.Core.Resources.Versioning;

namespace Calamari.Integration.Packages.Download
{
    /// <summary>
    /// This class knows how to interpret a package id and request a download
    /// from a specific downloader implementation. 
    /// </summary>
    public class PackageDownloaderStrategy : IPackageDownloader
    {
        static readonly IPackageIDParser mavenPackageIdParser = new MavenPackageIDParser();
        static readonly IPackageIDParser nugetPackageIdParser = new NuGetPackageIDParser();

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
            out string hash, out long size)
        {
            if (nugetPackageIdParser.CanGetMetadataFromPackageID(packageId, out var nugetMetadata))
            {
                new NuGetPackageDownloader().DownloadPackage(
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
            else if (mavenPackageIdParser.CanGetMetadataFromPackageID(packageId, out var mavenMetadata))
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}