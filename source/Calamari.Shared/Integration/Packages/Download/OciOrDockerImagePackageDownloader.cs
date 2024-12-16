using System;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Packages.Download.Oci;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    public class OciOrDockerImagePackageDownloader : IPackageDownloader
    {
        readonly OciPackageDownloader ociPackageDownloader;
        readonly DockerImagePackageDownloader dockerImagePackageDownloader;
        readonly OciClient ociClient;
        readonly ILog log;

        public OciOrDockerImagePackageDownloader(
            OciPackageDownloader ociPackageDownloader,
            DockerImagePackageDownloader dockerImagePackageDownloader,
            OciClient ociClient,
            ILog log)
        {
            this.ociPackageDownloader = ociPackageDownloader;
            this.dockerImagePackageDownloader = dockerImagePackageDownloader;
            this.log = log;
            this.ociClient = ociClient;
        }

        public PackagePhysicalFileMetadata DownloadPackage(
            string packageId,
            IVersion version,
            string feedId,
            Uri feedUri,
            string? feedUsername,
            string? feedPassword,
            bool forcePackageDownload,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            var downloader = GetInnerDownloader(packageId, version, feedUri, feedUsername, feedPassword);

            return downloader.DownloadPackage(
                packageId,
                version,
                feedId,
                feedUri,
                feedUsername,
                feedPassword,
                forcePackageDownload,
                maxDownloadAttempts,
                downloadAttemptBackoff);
        }

        IPackageDownloader GetInnerDownloader(string packageId, IVersion version, Uri feedUri, string? feedUsername, string? feedPassword)
        {
            var ociArtifactManifestRetriever = new OciArtifactManifestRetriever(ociClient, log);
            if (ociArtifactManifestRetriever.TryGetArtifactType(packageId, version, feedUri, feedUsername, feedPassword) == OciArtifactTypes.HelmChart)
            {
                return ociPackageDownloader;
            }

            return dockerImagePackageDownloader;
        }
    }
}