using System;
using System.Net;
using Calamari.Common.Features.Packages;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    /// <summary>
    /// Defines a service for downloading packages locally.
    /// </summary>
    public interface IPackageDownloader
    {
        /// <summary>
        /// Downloads the given file to the local cache.
        /// </summary>
        PackagePhysicalFileMetadata DownloadPackage(string packageId,
                                                    IVersion version,
                                                    string feedId,
                                                    Uri feedUri,
                                                    string? feedUsername,
                                                    string? feedPassword,
                                                    bool forcePackageDownload,
                                                    int maxDownloadAttempts,
                                                    TimeSpan downloadAttemptBackoff);
    }
}