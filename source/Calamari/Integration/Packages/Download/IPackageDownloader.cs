using System;
using System.Net;
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
        void DownloadPackage(
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
            out long size);
    }
}