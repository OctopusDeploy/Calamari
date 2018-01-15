using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Calamari.Exceptions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Java;
using Calamari.Util;
using Octopus.Core.Extensions;
using Octopus.Core.Util;
using Octopus.Versioning;
using Octopus.Versioning.Constants;
using Octopus.Versioning.Factories;
using Octopus.Versioning.Metadata;
using Octopus.Versioning.Parsing.Maven;

namespace Calamari.Integration.Packages.Download
{
    /// <summary>
    /// The Calamari Maven artifact downloader.
    /// </summary>
    public class MavenPackageDownloader : IPackageDownloader
    {
        static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();
        static readonly IPackageIDParser PackageIdParser = new MavenPackageIDParser();
        static readonly IVersionFactory VersionFactory = new VersionFactory();
        static readonly IMetadataParser MetadataParser = new MetadataParser();
        readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

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
            out string hash,
            out long size)
        {
            var cacheDirectory = PackageDownloaderUtils.GetPackageRoot(feedId);

            downloadedTo = null;
            if (!forcePackageDownload)
            {
                Log.Info("Attempting to get from cache");
                try
                {
                    downloadedTo = SourceFromCache(
                        packageId,
                        version,
                        cacheDirectory);
                }
                catch (Exception ex)
                {
                    Log.Info("SourceFromCache() failed");
                    Log.Info("Exception starts");
                    Log.Info(ex.ToString());
                    Log.Info(ex.StackTrace);
                    Log.Info("Exception ends");
                }
            }

            if (downloadedTo == null)
            {
                downloadedTo = DownloadPackage(
                    packageId,
                    version,
                    feedUri,
                    feedCredentials,
                    cacheDirectory,
                    maxDownloadAttempts,
                    downloadAttemptBackoff);
            }
            else
            {
                Log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloadedTo);
            }

            size = fileSystem.GetFileSize(downloadedTo);
            hash = downloadedTo
                .Map(path => FunctionalExtensions.Using(
                    () => fileSystem.OpenFile(path, FileAccess.Read),
                    stream => HashCalculator.Hash(stream)));
        }

        /// <summary>
        /// Matches a file to a package id and version
        /// </summary>
        /// <param name="file">The path of the file we are checking</param>
        /// <param name="packageId">The desired package id</param>
        /// <param name="version">The desired version</param>
        /// <returns>true if the file matches the pacakge id and version, and false otherwise</returns>
        bool FileMatchesDetails(string file, string packageId, IVersion version)
        {
            return PackageIdParser.TryGetMetadataFromServerPackageName(file).ToEnumerable()
                .Where(meta => meta != Maybe<PackageMetadata>.None)
                .Where(meta => meta.Value.PackageId == packageId)
                .Any(meta => VersionFactory.TryCreateVersion(meta.Value.Version.ToString(),
                                 out IVersion packageVersion, meta.Value.VersionFormat) &&
                             version.Equals(packageVersion));
        }

        /// <summary>
        /// Attempt to find a package id and version in the local cache
        /// </summary>
        /// <param name="packageId">The desired package id</param>
        /// <param name="version">The desired version</param>
        /// <param name="cacheDirectory">The location of cached files</param>
        /// <returns>The path to a cached version of the file, or null if none are found</returns>
        string SourceFromCache(
            string packageId,
            IVersion version,
            string cacheDirectory)
        {
            Guard.NotNullOrWhiteSpace(packageId, "packageId can not be null");
            Guard.NotNull(version, "version can not be null");
            Guard.NotNullOrWhiteSpace(cacheDirectory, "cacheDirectory can not be null");

            Log.VerboseFormat("Checking package cache for package {0} {1}", packageId, version.ToString());

            fileSystem.EnsureDirectoryExists(cacheDirectory);

            var filename = new MavenPackageID(packageId).FileSystemName;

            return JarExtractor.EXTENSIONS
                .Select(extension => filename + "*" + extension)
                // Convert the search pattern to matching file paths
                .SelectMany(searchPattern => fileSystem.EnumerateFilesRecursively(cacheDirectory, searchPattern))
                // Filter out unparseable and unmatched results
                .FirstOrDefault(file => FileMatchesDetails(file, packageId, version));
        }

        /// <summary>
        /// Downloads the artifact from the Maven repo. This method first checks the repo for
        /// artifacts with all available extensions, as we have no indication what type of artifact
        /// (jar, war, zip etc) that we are attempting to download.
        /// </summary>
        /// <param name="packageId">The package id</param>
        /// <param name="version">The package version</param>
        /// <param name="feedUri">The maven repo uri</param>
        /// <param name="feedCredentials">The mavben repo credentials</param>
        /// <param name="cacheDirectory">The directory to download the file into</param>
        /// <param name="maxDownloadAttempts">How many times to try the download</param>
        /// <param name="downloadAttemptBackoff">How long to wait between attempts</param>
        /// <returns>The path to the downloaded artifact</returns>
        string DownloadPackage(
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

            Log.Info("Downloading Maven package {0} {1} from feed: '{2}'", packageId, version, feedUri);
            Log.VerboseFormat("Downloaded package will be stored in: '{0}'", cacheDirectory);
            fileSystem.EnsureDirectoryExists(cacheDirectory);
            fileSystem.EnsureDiskHasEnoughFreeSpace(cacheDirectory);

            var mavenPackageId = new MavenPackageID(packageId, version);
            
            var snapshotMetadata = GetSnapshotMetadata(
                mavenPackageId, 
                feedUri, 
                feedCredentials, 
                maxDownloadAttempts,
                downloadAttemptBackoff);

            return FirstToRespond(mavenPackageId, feedUri, feedCredentials, snapshotMetadata)
                .Tee(mavenGavFirst => Log.VerboseFormat("Found package {0} version {1}", packageId, version))
                .Map(mavenGavFirst => DownloadArtifact(
                    mavenGavFirst,
                    packageId,
                    version,
                    feedUri,
                    feedCredentials,
                    cacheDirectory,
                    maxDownloadAttempts,
                    downloadAttemptBackoff,
                    snapshotMetadata));
        }

        /// <summary>
        /// Actually download the maven file.
        /// </summary>
        /// <returns>The path to the downloaded file</returns>
        string DownloadArtifact(
            MavenPackageID mavenGavFirst,
            string packageId,
            IVersion version,
            Uri feedUri,
            ICredentials feedCredentials,
            string cacheDirectory,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff,
            XmlDocument snapshotMetadata)
        {
            Guard.NotNull(mavenGavFirst, "mavenGavFirst can not be null");
            Guard.NotNullOrWhiteSpace(packageId, "packageId can not be null");
            Guard.NotNull(version, "version can not be null");
            Guard.NotNullOrWhiteSpace(cacheDirectory, "cacheDirectory can not be null");
            Guard.NotNull(feedUri, "feedUri can not be null");           

            for (var retry = 0; retry < maxDownloadAttempts; ++retry)
            {
                try
                {
                    return GetFilePathToDownloadPackageTo(
                            cacheDirectory,
                            packageId,
                            version.ToString(),
                            mavenGavFirst.Packaging)
                        .Tee(path => feedUri.ToString().TrimEnd('/')
                            .Map(uri => uri + (snapshotMetadata == null ? 
                                            mavenGavFirst.DefaultArtifactPath : 
                                            mavenGavFirst.SnapshotArtifactPath(MetadataParser.GetLatestSnapshotRelease(
                                                snapshotMetadata, 
                                                mavenGavFirst.Packaging,
                                                mavenGavFirst.Version))))
                            .Map(uri => FunctionalExtensions.Using(
                                () => new WebClient(),
                                client => client
                                    .Tee(c => c.Credentials = feedCredentials)
                                    .Tee(c => c.DownloadFile(uri, path))))
                        );
                }
                catch
                {
                    Thread.Sleep(downloadAttemptBackoff);
                }
            }

            throw new MavenDownloadException("Failed to download the Maven artifact");
        }

        /// <summary>
        /// Find the first artifact to respond to a HTTP head request. We use this to find the extension
        /// of the artifact that we are trying to download.
        /// </summary>
        /// <returns>The details of the first (and only) artifact to respond to a head request</returns>
        MavenPackageID FirstToRespond(
            MavenPackageID mavenPackageId, 
            Uri feedUri, 
            ICredentials feedCredentials,
            XmlDocument snapshotMetadata)
        {
            Guard.NotNull(mavenPackageId, "mavenPackageId can not be null");
            Guard.NotNull(feedUri, "feedUri can not be null");

            return JarExtractor.EXTENSIONS
                       .AsParallel()
                       .Select(extension => new MavenPackageID(
                           mavenPackageId.Group,
                           mavenPackageId.Artifact,
                           mavenPackageId.Version,
                           Regex.Replace(extension, "^\\.", "")))
                       .FirstOrDefault(mavenGavParser => MavenPackageExists(mavenGavParser, feedUri, feedCredentials, snapshotMetadata))
                   ?? throw new MavenDownloadException("Failed to find the Maven artifact");
        }

        /// <summary>
        /// Performs the actual HTTP head request to check for the presence of a maven artifact with a 
        /// given extension.
        /// </summary>
        /// <returns>true if the package exists, and false otherwise</returns>
        bool MavenPackageExists(MavenPackageID mavenGavParser, Uri feedUri, ICredentials feedCredentials, XmlDocument snapshotMetadata)
        {
            return feedUri.ToString().TrimEnd('/')
                .Map(uri => uri + (snapshotMetadata == null ?
                                mavenGavParser.DefaultArtifactPath : 
                                mavenGavParser.SnapshotArtifactPath(MetadataParser.GetLatestSnapshotRelease(
                                    snapshotMetadata, 
                                    mavenGavParser.Packaging,
                                    mavenGavParser.Version))))
                .Map(uri =>
                {
                    try
                    {
                        return WebRequest.Create(uri)
                            .Tee(c => c.Method = "HEAD")
                            .Tee(c => c.Credentials = feedCredentials)
                            .GetResponse()
                            .Map(response => response as HttpWebResponse)
                            .Map(response => (int) response.StatusCode >= 200 && (int) response.StatusCode <= 299);
                    }
                    catch
                    {
                        return false;
                    }
                });
        }

        /// <summary>
        /// Creates the full file name of a downloaded file
        /// </summary>
        /// <returns>The full path where the downloaded file will be saved</returns>
        string GetFilePathToDownloadPackageTo(string cacheDirectory, string packageId, string version, string extension)
        {
            Guard.NotNullOrWhiteSpace(cacheDirectory, "cacheDirectory can not be null");
            Guard.NotNullOrWhiteSpace(packageId, "packageId can not be null");
            Guard.NotNullOrWhiteSpace(version, "version can not be null");
            Guard.NotNullOrWhiteSpace(extension, "extension can not be null");

            return (packageId + JavaConstants.MavenFilenameDelimiter + version +
                    ServerConstants.SERVER_CACHE_DELIMITER +
                    BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty) +
                    "." + extension)
                .Map(package => Path.Combine(cacheDirectory, package));
        }

        /// <summary>
        /// Attempt to get the snapshot maven-metadata.xml file, which we will need to use to build up
        /// the filenames of snapshot versions.
        /// </summary>
        /// <returns>The snapshot maven-metadata.xml file if it exists, and a null result otherwise</returns>
        XmlDocument GetSnapshotMetadata(
            MavenPackageID mavenPackageID, 
            Uri feedUri,
            ICredentials feedCredentials,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            for (var retry = 0; retry < maxDownloadAttempts; ++retry)
            {
                try
                {
                    var metadataResponse = (feedUri.ToString().TrimEnd('/') + mavenPackageID.GroupVersionMetadataPath)
                        .ToEnumerable()
                        .Select(uri => WebRequest.Create(uri).Tee(request => request.Credentials = feedCredentials))
                        .Select(request => request.GetResponse())
                        .Select(response => response as HttpWebResponse)
                        .First(response => response.IsSuccessStatusCode() || (int) response.StatusCode == 404);

                    if (metadataResponse.IsSuccessStatusCode())
                    {
                        return FunctionalExtensions.Using(
                            () => metadataResponse.GetResponseStream(),
                            stream => new XmlDocument().Tee(doc => doc.Load(stream)));
                    }

                    return null;
                }
                catch (WebException ex)
                {
                    if (ex.Response is HttpWebResponse response)
                    {
                        if ((int)(response.StatusCode) == 404)
                        {
                            return null;
                        }
                    }
                    
                    Thread.Sleep(downloadAttemptBackoff);
                }
                catch 
                {
                    Thread.Sleep(downloadAttemptBackoff);
                }
            }

            throw new MavenDownloadException("Failed to download the Maven artifact");
        }
    }
}