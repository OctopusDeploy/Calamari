using System;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Packages.Download.Oci;
using Newtonsoft.Json.Linq;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    public enum OciArtifactTypes
    {
        DockerImage,
        HelmChart,
        Unknown
    }

    public class OciPackageDownloader : IPackageDownloader
    {
        static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();
        readonly ICalamariFileSystem fileSystem;
        readonly ICombinedPackageExtractor combinedPackageExtractor;
        readonly ILog log;
        readonly OciRegistryClient ociRegistryClient;

        public OciPackageDownloader(
            ICalamariFileSystem fileSystem,
            ICombinedPackageExtractor combinedPackageExtractor,
            OciRegistryClient ociRegistryClient,
            ILog log)
        {
            this.fileSystem = fileSystem;
            this.combinedPackageExtractor = combinedPackageExtractor;
            this.ociRegistryClient = ociRegistryClient;
            this.log = log;
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
            var cacheDirectory = PackageDownloaderUtils.GetPackageRoot(feedId);
            fileSystem.EnsureDirectoryExists(cacheDirectory);

            if (!forcePackageDownload)
            {
                var downloaded = SourceFromCache(packageId, version, cacheDirectory);
                if (downloaded != null)
                {
                    log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloaded.FullFilePath);
                    return downloaded;
                }
            }

            var tempDirectory = fileSystem.CreateTemporaryDirectory();

            using (new TemporaryDirectory(tempDirectory))
            {
                var homeDir = Path.Combine(tempDirectory, "oci");
                if (!Directory.Exists(homeDir))
                {
                    Directory.CreateDirectory(homeDir);
                }

                var stagingDir = Path.Combine(homeDir, "staging");
                if (!Directory.Exists(stagingDir))
                {
                    Directory.CreateDirectory(stagingDir);
                }

                var (digest, size, extension) = GetPackageDetails(feedUri, packageId, version, feedUsername, feedPassword);
                var hash = OciRegistryClient.GetPackageHashFromDigest(digest);

                var cachedFileName = PackageName.ToCachedFileName(packageId, version, extension);
                var downloadPath = Path.Combine(Path.Combine(stagingDir, cachedFileName));

                var retryStrategy = PackageDownloaderRetryUtils.CreateRetryStrategy<CommandException>(maxDownloadAttempts, downloadAttemptBackoff, log);
                retryStrategy.Execute(() => ociRegistryClient.DownloadPackage(feedUri, packageId, digest, feedUsername, feedPassword, downloadPath));

                var localDownloadName = Path.Combine(cacheDirectory, cachedFileName);
                fileSystem.MoveFile(downloadPath, localDownloadName);

                return !string.IsNullOrEmpty(hash)
                    ? new PackagePhysicalFileMetadata(
                        PackageName.FromFile(localDownloadName),
                        localDownloadName,
                        hash,
                        size)
                    : PackagePhysicalFileMetadata.Build(localDownloadName)
                      ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
            }
        }

        (string digest, int size, string extension) GetPackageDetails(
            Uri feedUri,
            string packageId,
            IVersion version,
            string? feedUserName,
            string? feedPassword)
        {
            var manifest = ociRegistryClient.GetManifest(feedUri, packageId, version, feedUserName, feedPassword);

            var layer = manifest.Value<JArray>(OciConstants.Manifest.Layers.PropertyName)[0];
            var digest = layer.Value<string>(OciConstants.Manifest.Layers.DigestPropertyName);
            var size = layer.Value<int>(OciConstants.Manifest.Layers.SizePropertyName);
            var extension = GetExtensionFromManifest(layer);

            return (digest, size, extension);
        }

        string GetExtensionFromManifest(JToken layer)
        {
            var artifactTitle = layer.Value<JObject>(OciConstants.Manifest.Layers.AnnotationsPropertyName)?[OciConstants.Manifest.Image.TitleAnnotationKey]?.Value<string>() ?? "";
            var extension = combinedPackageExtractor
                            .Extensions
                            .FirstOrDefault(
                                ext =>
                                    Path.GetExtension(artifactTitle).Equals(ext, StringComparison.OrdinalIgnoreCase));

            return extension ?? (layer.Value<string>(OciConstants.Manifest.Layers.MediaTypePropertyName).EndsWith("tar+gzip") ? ".tgz" : ".tar");
        }

        PackagePhysicalFileMetadata? SourceFromCache(string packageId, IVersion version, string cacheDirectory)
        {
            log.VerboseFormat("Checking package cache for package {0} v{1}", packageId, version.ToString());

            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version, combinedPackageExtractor.Extensions));

            foreach (var file in files)
            {
                var package = PackageName.FromFile(file);
                if (package == null)
                    continue;

                if (string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase) && package.Version.Equals(version))
                {
                    var packagePhysicalFileMetadata = PackagePhysicalFileMetadata.Build(file, package)
                                                      ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
                    return packagePhysicalFileMetadata;
                }
            }

            return null;
        }
    }
}