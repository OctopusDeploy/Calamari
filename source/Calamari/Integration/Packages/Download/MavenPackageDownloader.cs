using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Calamari.Constants;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Java;
using Calamari.Util;
using Octopus.Core.Constants;
using Octopus.Core.Extensions;
using Octopus.Core.Resources;
using Octopus.Core.Resources.Metadata;
using Octopus.Core.Resources.Parsing.Maven;
using Octopus.Core.Resources.Versioning;
using Octopus.Core.Resources.Versioning.Factories;
using Octopus.Core.Util;
using JavaConstants = Octopus.Core.Constants.JavaConstants;

namespace Calamari.Integration.Packages.Download
{
    public class MavenPackageDownloader : IPackageDownloader
    {
        static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();
        static readonly IMavenURLParser MavenUrlParser = new MavenURLParser();
        static readonly IPackageIDParser PackageIdParser = new MavenPackageIDParser();
        static readonly IVersionFactory VersionFactory = new VersionFactory();
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

        bool FileMatchesDetails(string file, string packageId, IVersion version)
        {
            return PackageIdParser.CanGetMetadataFromServerPackageName(file).ToEnumerable()
                .Where(meta => meta != Maybe<PackageMetadata>.None)
                .Where(meta => meta.Value.PackageId == packageId)
                .Any(meta => VersionFactory.CanCreateVersion(meta.Value.Version.ToString(),
                                 out IVersion packageVersion, meta.Value.FeedType) &&
                             version.Equals(packageVersion));
        }

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

            return new MavenPackageID(packageId, version)
                .Map(mavenPackageId => FirstToRespond(mavenPackageId, feedUri, feedCredentials))
                .Tee(mavenGavFirst => Log.VerboseFormat("Found package {0} version {1}", packageId, version))
                .Map(mavenGavFirst => DownloadArtifact(
                    mavenGavFirst,
                    packageId,
                    version,
                    feedUri,
                    feedCredentials,
                    cacheDirectory,
                    maxDownloadAttempts,
                    downloadAttemptBackoff));
        }

        string DownloadArtifact(
            MavenPackageID mavenGavFirst,
            string packageId,
            IVersion version,
            Uri feedUri,
            ICredentials feedCredentials,
            string cacheDirectory,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            Guard.NotNull(mavenGavFirst, "mavenGavFirst can not be null");
            Guard.NotNullOrWhiteSpace(packageId, "packageId can not be null");
            Guard.NotNull(version, "version can not be null");
            Guard.NotNullOrWhiteSpace(cacheDirectory, "cacheDirectory can not be null");
            Guard.NotNull(feedUri, "feedUri can not be null");
            
            return GetFilePathToDownloadPackageTo(
                    cacheDirectory,
                    packageId,
                    version.ToString(),
                    mavenGavFirst.Packaging)
                .Tee(path => MavenUrlParser.SanitiseFeedUri(feedUri).ToString().TrimEnd('/')
                    .Map(uri => uri + mavenGavFirst.ArtifactPath)
                    .Map(uri => FunctionalExtensions.Using(
                        () => new WebClient(), 
                        client => client
                            .Tee(c => c.Credentials = feedCredentials)
                            .Tee(c => c.DownloadFile(uri, path))))
                );
        }

        MavenPackageID FirstToRespond(MavenPackageID mavenPackageId, Uri feedUri, ICredentials feedCredentials)
        {
            Guard.NotNull(mavenPackageId, "mavenPackageId can not be null");
            Guard.NotNull(feedUri, "feedUri can not be null");
                        
            Log.Info("FirstToRespond - start");

            var retValue = JarExtractor.EXTENSIONS
                .Select(extension => new MavenPackageID(
                    mavenPackageId.Group,
                    mavenPackageId.Artifact,
                    mavenPackageId.Version,
                    Regex.Replace(extension, "^\\.", "")))
                .FirstOrDefault(mavenGavParser => MavenPackageExists(mavenGavParser, feedUri, feedCredentials))
                ?? throw new Exception("Failed to find the maven artifact");
            
            Log.Info("FirstToRespond - end");
            return retValue;
        }

        bool MavenPackageExists(MavenPackageID mavenGavParser, Uri feedUri, ICredentials feedCredentials)
        {
            Log.Info("MavenPackageExists - start");
            
            var retValue = MavenUrlParser.SanitiseFeedUri(feedUri).ToString().TrimEnd('/')
                .Map(uri => uri + mavenGavParser.ArtifactPath)
                .Map(uri => (HttpWebResponse)WebRequest.Create(uri).Tee(c => c.Method = "HEAD").GetResponse())
                .Map(response => (int)response.StatusCode >= 200 && (int)response.StatusCode <= 299);
            
            Log.Info("MavenPackageExists - end");
            return retValue;
        }

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
    }
}