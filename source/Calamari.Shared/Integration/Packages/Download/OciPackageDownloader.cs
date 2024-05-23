using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Newtonsoft.Json;
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
        readonly HttpClient client;

        public OciPackageDownloader(
            ICalamariFileSystem fileSystem,
            ICombinedPackageExtractor combinedPackageExtractor)
        {
            this.fileSystem = fileSystem;
            this.combinedPackageExtractor = combinedPackageExtractor;
            client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None });
        }
        
        public PackagePhysicalFileMetadata DownloadPackage(string packageId,
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
                    Log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloaded.FullFilePath);
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

                var versionString = Oci.FixVersion(version);

                var apiUrl = Oci.GetApiUri(feedUri);
                var (digest, size, extension) = GetPackageDetails(apiUrl, packageId, versionString, feedUsername, feedPassword);
                var hash = Oci.GetPackageHashFromDigest(digest);

                var cachedFileName = PackageName.ToCachedFileName(packageId, version, extension);
                var downloadPath = Path.Combine(Path.Combine(stagingDir, cachedFileName));

                DownloadPackage(apiUrl, packageId, digest, feedUsername, feedPassword, downloadPath);

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
            Uri url,
            string packageId,
            string version,
            string? feedUserName, 
            string? feedPassword)
        {
            var manifest = Oci.GetManifest(client, url, packageId, version, feedUserName, feedPassword);

            var layer = manifest.Value<JArray>(Oci.Manifest.Layer.PropertyName)[0];
            var digest = layer.Value<string>(Oci.Manifest.Layer.DigestPropertyName);
            var size = layer.Value<int>(Oci.Manifest.Layer.SizePropertyName);
            var extension = GetExtensionFromManifest(layer);

            return (digest, size, extension);
        }

        string GetExtensionFromManifest(JToken layer)
        {
            var artifactTitle = layer.Value<JObject>(Oci.Manifest.Layer.AnnotationsPropertyName)?[Oci.Manifest.Image.TitleAnnotationKey]?.Value<string>() ?? "";
            var extension = combinedPackageExtractor
                .Extensions
                .FirstOrDefault(ext => 
                    Path.GetExtension(artifactTitle).Equals(ext, StringComparison.OrdinalIgnoreCase));

            return extension ?? (layer.Value<string>(Oci.Manifest.Layer.MediaTypePropertyName).EndsWith("tar+gzip") ? ".tgz" : ".tar");
        }

        void DownloadPackage(
            Uri url,
            string packageId,
            string digest,
            string? feedUsername,
            string? feedPassword,
            string downloadPath)
        {
            using var fileStream = fileSystem.OpenFile(downloadPath, FileAccess.Write);
            using var response = Oci.Get(client, new Uri($"{url}/{packageId}/blobs/{digest}"), new NetworkCredential(feedUsername, feedPassword));
            if (!response.IsSuccessStatusCode)
            {
                throw new CommandException(
                    $"Failed to download artifact (Status Code {(int)response.StatusCode}). Reason: {response.ReasonPhrase}");
            }
            
            response.Content.CopyToAsync(fileStream).GetAwaiter().GetResult();
        }
        
        PackagePhysicalFileMetadata? SourceFromCache(string packageId, IVersion version, string cacheDirectory)
        {
            Log.VerboseFormat("Checking package cache for package {0} v{1}", packageId, version.ToString());

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