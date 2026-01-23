using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
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

        readonly ICalamariFileSystem fileSystem;

        public NpmPackageDownloader(ICalamariFileSystem fileSystem)
        {
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
                    Log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloaded.FullFilePath);
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
            Log.VerboseFormat("Checking package cache for package {0} v{1}", packageId, version.ToString());

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

            Log.Info("Downloading NPM package {0} v{1} from feed: '{2}'", packageId, version, feedUri);
            Log.VerboseFormat("Downloaded package will be stored in: '{0}'", cacheDirectory);

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

            for (var retry = 0; retry < maxDownloadAttempts; ++retry)
            {
                try
                {
                    using (var handler = new HttpClientHandler())
                    {
                        handler.Credentials = feedCredentials;

                        using (var client = new HttpClient(handler))
                        {
                            Log.VerboseFormat("Fetching NPM package metadata from {0}", metadataUrl);
                            var response = client.GetAsync(metadataUrl).GetAwaiter().GetResult();
                            response.EnsureSuccessStatusCode();

                            var metadataJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

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

                                Log.VerboseFormat("Found tarball URL: {0}", tarballUrl);
                                return tarballUrl;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if ((retry + 1) == maxDownloadAttempts)
                    {
                        throw new CommandException($"Failed to fetch NPM package metadata after {maxDownloadAttempts} attempts.\r\nLast Exception Message: {ex.Message}", ex);
                    }
                    Log.VerboseFormat("Attempt {0} failed to fetch metadata, retrying in {1} seconds. Error: {2}", retry + 1, downloadAttemptBackoff.TotalSeconds, ex.Message);
                    Thread.Sleep(downloadAttemptBackoff);
                }
            }

            throw new CommandException("Failed to fetch NPM package metadata");
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
            for (var retry = 0; retry < maxDownloadAttempts; ++retry)
            {
                try
                {
                    Log.VerboseFormat("Downloading NPM package from {0} to {1}", tarballUrl, localDownloadName);

                    using (var handler = new HttpClientHandler())
                    {
                        handler.Credentials = feedCredentials;

                        using (var client = new HttpClient(handler))
                        {
                            var response = client.GetAsync(tarballUrl).GetAwaiter().GetResult();
                            response.EnsureSuccessStatusCode();

                            using (var fileStream = File.Create(localDownloadName))
                            {
                                response.Content.CopyToAsync(fileStream).GetAwaiter().GetResult();
                            }

                            var packagePhysicalFileMetadata = PackagePhysicalFileMetadata.Build(localDownloadName);
                            return packagePhysicalFileMetadata
                                ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if ((retry + 1) == maxDownloadAttempts)
                    {
                        throw new CommandException($"Failed to download NPM package after {maxDownloadAttempts} attempts.\r\nLast Exception Message: {ex.Message}", ex);
                    }
                    Log.VerboseFormat("Attempt {0} failed to download package, retrying in {1} seconds. Error: {2}", retry + 1, downloadAttemptBackoff.TotalSeconds, ex.Message);
                    Thread.Sleep(downloadAttemptBackoff);
                }
            }

            throw new CommandException("Failed to download NPM package");
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
