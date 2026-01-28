using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Octopus.Versioning;

namespace Calamari.Integration.Packages.Download
{
    /// <summary>
    /// The Calamari NPM package downloader.
    /// </summary>
    public class NpmPackageDownloader : IPackageDownloader
    {
        static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();

        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;

        public NpmPackageDownloader(ILog log, ICalamariFileSystem fileSystem)
        {
            this.log = log;
            this.fileSystem = fileSystem;
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
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
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

            return DownloadPackage(
                packageId,
                version,
                feedUri,
                GetFeedCredentials(feedUsername, feedPassword),
                cacheDirectory,
                maxDownloadAttempts,
                downloadAttemptBackoff);
        }

        /// <summary>
        /// Attempt to find a package id and version in the local cache
        /// </summary>
        /// <param name="packageId">The desired package id</param>
        /// <param name="version">The desired version</param>
        /// <param name="cacheDirectory">The location of cached files</param>
        /// <returns>The path to a cached version of the file, or null if none are found</returns>
        PackagePhysicalFileMetadata? SourceFromCache(string packageId, IVersion version, string cacheDirectory)
        {
            log.VerboseFormat("Checking package cache for package {0} v{1}", packageId, version.ToString());

            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version, [".tgz"]));

            foreach (var file in files)
            {
                var package = PackageName.FromFile(file);
                if (package == null)
                    continue;

                var idMatches = string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase);
                var versionExactMatch = string.Equals(package.Version.ToString(), version.ToString(), StringComparison.OrdinalIgnoreCase);
                var nugetVerMatches = package.Version.Equals(version);

                if (idMatches && (nugetVerMatches || versionExactMatch))
                    return PackagePhysicalFileMetadata.Build(file, package);
            }

            return null;
        }

        /// <summary>
        /// Downloads the NPM package from the registry.
        /// </summary>
        /// <param name="packageId">The package id</param>
        /// <param name="version">The package version</param>
        /// <param name="feedUri">The npm registry uri</param>
        /// <param name="feedCredentials">The npm registry credentials</param>
        /// <param name="cacheDirectory">The directory to download the file into</param>
        /// <param name="maxDownloadAttempts">How many times to try the download</param>
        /// <param name="downloadAttemptBackoff">How long to wait between attempts</param>
        /// <returns>The path to the downloaded package</returns>
        PackagePhysicalFileMetadata DownloadPackage(
            string packageId,
            IVersion version,
            Uri feedUri,
            ICredentials feedCredentials,
            string cacheDirectory,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            Guard.NotNullOrWhiteSpace(packageId, "packageId can not be null");
            Guard.NotNull(version, "version can not be null");
            Guard.NotNullOrWhiteSpace(cacheDirectory, "cacheDirectory can not be null");
            Guard.NotNull(feedUri, "feedUri can not be null");

            log.InfoFormat("Downloading NPM package {0} v{1} from feed: '{2}'", packageId, version, feedUri);
            log.VerboseFormat("Downloaded package will be stored in: '{0}'", cacheDirectory);

            var tarballUrl = GetTarballUrl(packageId, version, feedUri, feedCredentials, maxDownloadAttempts, downloadAttemptBackoff);

            var localDownloadName = Path.Combine(cacheDirectory, PackageName.ToCachedFileName(packageId, version, ".tgz"));

            return DownloadTarball(
                tarballUrl,
                localDownloadName,
                packageId,
                version,
                feedCredentials,
                maxDownloadAttempts,
                downloadAttemptBackoff);
        }

        /// <summary>
        /// Get the tarball URL from the NPM package metadata
        /// </summary>
        string GetTarballUrl(
            string packageId,
            IVersion version,
            Uri feedUri,
            ICredentials feedCredentials,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            var encodedPackageId = Uri.EscapeDataString(packageId);
            var metadataUrl = $"{feedUri.ToString().TrimEnd('/')}/{encodedPackageId}";

            string metadataJson = null;

            var retryStrategy = PackageDownloaderRetryUtils.CreateRetryStrategy<HttpRequestException>(maxDownloadAttempts, downloadAttemptBackoff, log);
            retryStrategy.Execute(() =>
            {
                using (var handler = new HttpClientHandler())
                {
                    handler.Credentials = feedCredentials;

                    using (var client = new HttpClient(handler))
                    {
                        log.VerboseFormat("Fetching NPM package metadata from {0}", metadataUrl);
                        var response = client.GetAsync(metadataUrl).GetAwaiter().GetResult();

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new HttpRequestException($"Failed to fetch NPM package metadata (Status Code {(int)response.StatusCode}). Reason: {response.ReasonPhrase}");
                        }

                        metadataJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    }
                }
            });

            // Parse and validate the metadata outside the retry block
            // Validation errors are not transient and should not be retried
            using (var doc = JsonDocument.Parse(metadataJson))
            {
                var root = doc.RootElement;
                var versionString = version.OriginalString ?? version.ToString();

                if (!root.TryGetProperty("versions", out var versions) ||
                    !versions.TryGetProperty(versionString ?? string.Empty, out var versionInfo) ||
                    !versionInfo.TryGetProperty("dist", out var dist) ||
                    !dist.TryGetProperty("tarball", out var tarball))
                {
                    throw new CommandException($"Unable to find tarball URL for NPM package {packageId} version {version} in metadata response");
                }

                var tarballUrl = tarball.GetString();
                if (string.IsNullOrWhiteSpace(tarballUrl))
                {
                    throw new CommandException($"Tarball URL is empty for NPM package {packageId} version {version}");
                }

                log.VerboseFormat("Found tarball URL: {0}", tarballUrl);
                return tarballUrl;
            }
        }

        /// <summary>
        /// Download the NPM tarball
        /// </summary>
        PackagePhysicalFileMetadata DownloadTarball(
            string tarballUrl,
            string localDownloadName,
            string packageId,
            IVersion version,
            ICredentials feedCredentials,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            log.VerboseFormat("Downloading NPM package from {0} to {1}", tarballUrl, localDownloadName);

            var retryStrategy = PackageDownloaderRetryUtils.CreateRetryStrategy<HttpRequestException>(maxDownloadAttempts, downloadAttemptBackoff, log);
            retryStrategy.Execute(() =>
            {
                using (var handler = new HttpClientHandler())
                {
                    handler.Credentials = feedCredentials;

                    using (var client = new HttpClient(handler))
                    {
                        var response = client.GetAsync(tarballUrl).GetAwaiter().GetResult();

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new HttpRequestException($"Failed to download NPM package (Status Code {(int)response.StatusCode}). Reason: {response.ReasonPhrase}");
                        }

                        using (var fileStream = File.Create(localDownloadName))
                        {
                            response.Content.CopyToAsync(fileStream).GetAwaiter().GetResult();
                        }
                    }
                }
            });

            var packagePhysicalFileMetadata = PackagePhysicalFileMetadata.Build(localDownloadName);
            return packagePhysicalFileMetadata
                ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
        }

        static ICredentials GetFeedCredentials(string? feedUsername, string? feedPassword)
        {
            ICredentials credentials = CredentialCache.DefaultNetworkCredentials;
            if (!string.IsNullOrWhiteSpace(feedUsername))
            {
                credentials = new NetworkCredential(feedUsername, feedPassword);
            }
            return credentials;
        }
    }
}
