using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Java;
using Calamari.Integration.Packages.NuGet;
using Calamari.Util;
using Octopus.Core.Extensions;
using Octopus.Core.Resources.Parsing.Maven;
using Octopus.Core.Resources.Versioning;

namespace Calamari.Integration.Packages.Download
{
    public class MavenPackageDownloader : IPackageDownloader
    {
        private static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();
        private static readonly IMavenURLParser MavenUrlParser = new MavenURLParser();
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
                AttemptToGetPackageFromCache(
                    packageId,
                    version,
                    cacheDirectory,
                    out downloadedTo);
            }

            if (downloadedTo == null)
            {
                DownloadPackage(
                    packageId,
                    version,
                    feedUri,
                    feedCredentials,
                    cacheDirectory,
                    maxDownloadAttempts,
                    downloadAttemptBackoff,
                    out downloadedTo);
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

        private void AttemptToGetPackageFromCache(
            string packageId,
            IVersion version,
            string cacheDirectory,
            out string downloadedTo)
        {
            downloadedTo = null;
        }

        private void DownloadPackage(
            string packageId,
            IVersion version,
            Uri feedUri,
            ICredentials feedCredentials,
            string cacheDirectory,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff,
            out string downloadedTo)
        {
            Log.Info("Downloading Maven package {0} {1} from feed: '{2}'", packageId, version, feedUri);
            Log.VerboseFormat("Downloaded package will be stored in: '{0}'", cacheDirectory);
            fileSystem.EnsureDirectoryExists(cacheDirectory);
            fileSystem.EnsureDiskHasEnoughFreeSpace(cacheDirectory);

            try
            {
                downloadedTo = new MavenPackageID(packageId, version)
                    .Map(mavenPackageId => FirstToRespond(mavenPackageId, feedUri))
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
            catch (Exception ex)
            {
                throw new Exception($"Unable to download package {packageId}", ex);
            }
        }

        string DownloadArtifact(
            MavenPackageID mavenGavFirst,
            string packageId,
            IVersion version,
            Uri feedUri,
            ICredentials feedCredentials,
            string cacheDirectory,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff) =>
            GetFilePathToDownloadPackageTo(
                    cacheDirectory,
                    packageId,
                    version.ToString(),
                    mavenGavFirst.Packaging)
                .Tee(path => FunctionalExtensions.Using(
                    () => fileSystem.OpenFile(path, FileAccess.Write),
                    myStream =>
                    {
                        return MavenUrlParser.SanitiseFeedUri(feedUri).ToString().TrimEnd('/')
                            .Map(uri => uri + mavenGavFirst.ArtifactPath)
                            .Map(uri => new HttpClient().GetAsync(uri).Result)
                            .Map(result => result.Content.ReadAsByteArrayAsync().Result)
                            .Tee(content => myStream.Write(content, 0, content.Length));
                    }
                ));

        MavenPackageID FirstToRespond(MavenPackageID mavenPackageId, Uri feedUri) =>
            JarExtractor.EXTENSIONS.AsParallel()
                .Select(extension => new MavenPackageID(
                    mavenPackageId.Group,
                    mavenPackageId.Artifact,
                    mavenPackageId.Version,
                    Regex.Replace(extension, "^\\.", "")))
                .FirstOrDefault(mavenGavParser =>
                {
                    return MavenUrlParser.SanitiseFeedUri(feedUri).ToString().TrimEnd('/')
                        .Map(uri => uri + mavenGavParser.ArtifactPath)
                        .Map(uri => new HttpRequestMessage(HttpMethod.Head, uri))
                        .Map(request => new HttpClient().SendAsync(request).Result)
                        .Map(result => result.IsSuccessStatusCode);
                }) ?? throw new Exception("Failed to find the maven artifact");

        string GetFilePathToDownloadPackageTo(string cacheDirectory, string packageId, string version, string extension)
        {
            return (packageId + "." + version + "_" +
                    BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty) +
                    "." + extension)
                .Map(package => Path.Combine(cacheDirectory, package));
        }
    }
}