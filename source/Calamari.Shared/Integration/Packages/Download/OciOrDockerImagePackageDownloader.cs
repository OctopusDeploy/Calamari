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
        readonly OciRegistryClient ociRegistryClient;
        readonly ILog log;

        public OciOrDockerImagePackageDownloader(
            OciPackageDownloader ociPackageDownloader,
            DockerImagePackageDownloader dockerImagePackageDownloader,
            OciRegistryClient ociRegistryClient,
            ILog log)
        {
            this.ociPackageDownloader = ociPackageDownloader;
            this.dockerImagePackageDownloader = dockerImagePackageDownloader;
            this.log = log;
            this.ociRegistryClient = ociRegistryClient;
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
            var ociArtifactManifestRetriever = new OciArtifactManifestRetriever(ociRegistryClient, log);
            if (ociArtifactManifestRetriever.TryGetArtifactType(packageId, version, feedUri, feedUsername, feedPassword) == OciArtifactTypes.HelmChart)
            {
                return ociPackageDownloader;
            }

            return dockerImagePackageDownloader;
        }
    }
}