using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    public class OciPackageDownloader : IPackageDownloader
    {
        const string VersionPath = "v2";
        const string OciImageManifestAcceptHeader = "application/vnd.oci.image.manifest.v1+json";
        const string ManifestImageTitleAnnotationKey = "org.opencontainers.image.title";
        const string ManifestLayerAnnotationsPropertyName = "annotations";
        const string ManifestLayerMediaTypePropertyName = "mediaType";

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
            Uri ociUri,
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

                var versionString = FixVersion(version);

                var feedUrl = GetApiUri(ociUri);
                var (digest, size, extension) = GetPackageDetails(feedUrl, packageId, versionString, feedUsername, feedPassword);
                var hash = GetPackageHashFromDigest(digest);

                var cachedFileName = PackageName.ToCachedFileName(packageId, version, extension);
                var downloadPath = Path.Combine(Path.Combine(stagingDir, cachedFileName));

                DownloadPackage(feedUrl, packageId, digest, feedUsername, feedPassword, downloadPath);

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

        static string FixVersion(IVersion version)
        {
            // oci registries don't support the '+' tagging
            // https://helm.sh/docs/topics/registries/#oci-feature-deprecation-and-behavior-changes-with-v380
            return version.ToString().Replace("+", "_");
        }
        static string? GetPackageHashFromDigest(string digest)
        {
            var matches = Regex.Match(digest, @"[A-Za-z0-9_+.-]+:(?<hash>[A-Fa-f0-9]+)"); 
            return matches.Groups["hash"]?.Value;
        }

        private (string digest, int size, string extension) GetPackageDetails(
            Uri url,
            string packageId,
            string version,
            string? feedUserName, 
            string? feedPassword)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/{packageId}/manifests/{version}");
            ApplyAuthorization(request, feedUserName, feedPassword);
            ApplyAccept(request);

            using var response = client.SendAsync(request).Result;
            var manifest = JsonConvert.DeserializeObject<JObject>(response.Content.ReadAsStringAsync().Result);

            var layer = manifest.Value<JArray>("layers")[0];
            var digest = layer.Value<string>("digest");
            var size = layer.Value<int>("size");
            var extension = GetExtensionFromManifest(layer);

            return (digest, size, extension);
        }

        string GetExtensionFromManifest(JToken layer)
        {
            var artifactTitle = layer.Value<JObject>(ManifestLayerAnnotationsPropertyName)?[ManifestImageTitleAnnotationKey]?.Value<string>() ?? "";
            var extension = combinedPackageExtractor
                .Extensions
                .FirstOrDefault(ext => 
                    Path.GetExtension(artifactTitle).Equals(ext, StringComparison.OrdinalIgnoreCase));

            return extension ?? (layer.Value<string>(ManifestLayerMediaTypePropertyName).EndsWith("tar+gzip") ? ".tgz" : ".tar");
        }

        void DownloadPackage(
            Uri url,
            string packageId,
            string digest,
            string? feedUsername,
            string? feedPassword,
            string downloadPath)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/{packageId}/blobs/{digest}");
            ApplyAuthorization(request, feedUsername, feedPassword);

            using var fileStream = fileSystem.OpenFile(downloadPath, FileAccess.Write);
            using var response = client.SendAsync(request).Result;
            if (!response.IsSuccessStatusCode)
            {
                throw new CommandException(
                    $"Failed to download artifact (Status Code {(int)response.StatusCode}). Reason: {response.ReasonPhrase}");
            }

#if NET40
            response.Content.CopyToAsync(fileStream).Wait();
#else
            response.Content.CopyToAsync(fileStream).GetAwaiter().GetResult();
#endif
        }

        private static void ApplyAccept(HttpRequestMessage request)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(OciImageManifestAcceptHeader));
        }

        private static void ApplyAuthorization(
            HttpRequestMessage request, 
            string? feedUserName,
            string? feedPassword)
        {
            if (!string.IsNullOrEmpty(feedUserName))
            {
                request.Headers.AddAuthenticationHeader(feedUserName, feedPassword);
            }
        }

        private static Uri GetApiUri(Uri feedUri)
        {
            var httpScheme = BuildScheme(IsPlainHttp(feedUri));
            var r = feedUri.ToString().Replace($"oci{Uri.SchemeDelimiter}", $"{httpScheme}{Uri.SchemeDelimiter}").TrimEnd('/');
            var uri = new Uri(r);
            if (!r.EndsWith("/" + VersionPath))
            {
                uri = new Uri(uri, VersionPath);
            }

            return uri;
        }

        static bool IsPlainHttp(Uri uri) 
            => uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        static string BuildScheme(bool isPlainHttp)
            => isPlainHttp ? Uri.UriSchemeHttp : Uri.UriSchemeHttps;

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
                    var packagePhysicalFileMetadata = PackagePhysicalFileMetadata.Build(file, package);
                    return packagePhysicalFileMetadata
                           ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
                }
            }

            return null;
        }
    }
}