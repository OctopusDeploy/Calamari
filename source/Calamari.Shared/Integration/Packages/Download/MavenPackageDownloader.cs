using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Packages.Java;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Exceptions;
using Octopus.CoreUtilities.Extensions;
using Octopus.Versioning;
using Octopus.Versioning.Maven;

namespace Calamari.Integration.Packages.Download
{
    /// <summary>
    /// The Calamari Maven artifact downloader.
    /// </summary>
    public class MavenPackageDownloader : IPackageDownloader
    {
        /// <summary>
        /// These are extensions that can be handled by extractors other than the Java one. We accept these
        /// because not all artifacts from Maven will be used by Java.
        /// </summary>
        public static readonly string[] AdditionalExtensions = { ".nupkg", ".tar.bz2", ".tar.bz", ".tbz", ".tgz", ".tar.gz", ".tar.Z", ".tar" };

        static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();

        readonly ICalamariFileSystem fileSystem;

        public MavenPackageDownloader(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
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

            return DownloadPackage(packageId,
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

            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version, JarPackageExtractor.SupportedExtensions));

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

            Log.Info("Downloading Maven package {0} v{1} from feed: '{2}'", packageId, version, feedUri);
            Log.VerboseFormat("Downloaded package will be stored in: '{0}'", cacheDirectory);
            var mavenPackageId = MavenPackageID.CreatePackageIdFromOctopusInput(packageId, version);

            var snapshotMetadata = GetSnapshotMetadata(mavenPackageId,
                feedUri,
                feedCredentials,
                maxDownloadAttempts,
                downloadAttemptBackoff);

            var found = FirstToRespond(mavenPackageId, feedUri, feedCredentials, snapshotMetadata);
            Log.VerboseFormat("Found package {0} v{1}", packageId, version);

            return DownloadArtifact(
                found,
                packageId,
                version,
                feedUri,
                feedCredentials,
                cacheDirectory,
                maxDownloadAttempts,
                downloadAttemptBackoff,
                snapshotMetadata);
        }

        /// <summary>
        /// Actually download the maven file.
        /// </summary>
        /// <returns>The path to the downloaded file</returns>
        PackagePhysicalFileMetadata DownloadArtifact(
            MavenPackageID mavenGavFirst,
            string packageId,
            IVersion version,
            Uri feedUri,
            ICredentials feedCredentials,
            string cacheDirectory,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff,
            XmlDocument? snapshotMetadata)
        {
            Guard.NotNull(mavenGavFirst, "mavenGavFirst can not be null");
            Guard.NotNullOrWhiteSpace(packageId, "packageId can not be null");
            Guard.NotNull(version, "version can not be null");
            Guard.NotNullOrWhiteSpace(cacheDirectory, "cacheDirectory can not be null");
            Guard.NotNull(feedUri, "feedUri can not be null");

            var localDownloadName = Path.Combine(cacheDirectory, PackageName.ToCachedFileName(packageId, version, "." + mavenGavFirst.Packaging));
            var downloadUrl = feedUri.ToString().TrimEnd('/') +
                (snapshotMetadata == null
                    ? mavenGavFirst.DefaultArtifactPath
                    : mavenGavFirst.SnapshotArtifactPath(GetLatestSnapshotRelease(
                        snapshotMetadata,
                        mavenGavFirst.Packaging,
                        mavenGavFirst.Classifier,
                        mavenGavFirst.Version)));

            for (var retry = 0; retry < maxDownloadAttempts; ++retry)
                try
                {
                    Log.Verbose($"Downloading Attempt {downloadUrl} TO {localDownloadName}");
                    using (var client = new WebClient
                        { Credentials = feedCredentials })
                    {
                        client.DownloadFile(downloadUrl, localDownloadName);
                        var packagePhysicalFileMetadata = PackagePhysicalFileMetadata.Build(localDownloadName);
                        return packagePhysicalFileMetadata
                            ?? throw new CommandException($"Unable to retrieve metadata for package {packageId}, version {version}");
                    }
                }
                catch (Exception ex)
                {
                    if ((retry + 1) == maxDownloadAttempts)
                        throw new MavenDownloadException("Failed to download the Maven artifact.\r\nLast Exception Message: " + ex.Message);
                    Thread.Sleep(downloadAttemptBackoff);
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
            XmlDocument? snapshotMetadata)
        {
            Guard.NotNull(mavenPackageId, "mavenPackageId can not be null");
            Guard.NotNull(feedUri, "feedUri can not be null");

            var errors = new ConcurrentBag<string>();
            var fileChecks = JarPackageExtractor.SupportedExtensions
                .Union(AdditionalExtensions)
                // Either consider all supported extensions, or select only the specified extension
                .Where(e => string.IsNullOrEmpty(mavenPackageId.Packaging) || e == "." + mavenPackageId.Packaging)
                .Select(extension =>
                {
                    var packageId = new MavenPackageID(
                        mavenPackageId.Group,
                        mavenPackageId.Artifact,
                        mavenPackageId.Version,
                        Regex.Replace(extension, "^\\.", ""),
                        mavenPackageId.Classifier);
                    var result = MavenPackageExists(packageId, feedUri, feedCredentials, snapshotMetadata);
                    errors.Add(result.ErrorMsg);
                    return new
                    {
                        result.Found,
                        MavenPackageId = packageId
                    };
                });

            var firstFound = fileChecks.FirstOrDefault(res => res.Found);
            if (firstFound != null)
                return firstFound.MavenPackageId;

            throw new MavenDownloadException($"Failed to find the Maven artifact.\r\nReceived Error(s):\r\n{string.Join("\r\n", errors.Distinct().ToList())}");
        }

        /// <summary>
        /// Performs the actual HTTP head request to check for the presence of a maven artifact with a
        /// given extension.
        /// </summary>
        /// <returns>true if the package exists, and false otherwise</returns>
        (bool Found, string ErrorMsg) MavenPackageExists(MavenPackageID mavenGavParser, Uri feedUri, ICredentials feedCredentials, XmlDocument? snapshotMetadata)
        {
            var uri = feedUri.ToString().TrimEnd('/') +
                (snapshotMetadata == null
                    ? mavenGavParser.DefaultArtifactPath
                    : mavenGavParser.SnapshotArtifactPath(
                        GetLatestSnapshotRelease(
                            snapshotMetadata,
                            mavenGavParser.Packaging,
                            mavenGavParser.Classifier,
                            mavenGavParser.Version)));

            try
            {
                var req = WebRequest.Create(uri);
                req.Method = "HEAD";
                req.Credentials = feedCredentials;
                using (var response = (HttpWebResponse)req.GetResponse())
                {
                    return ((int)response.StatusCode >= 200 && (int)response.StatusCode <= 299, $"Unexpected Response: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to download {uri}\n" + ex.Message);
            }
        }

        /// <summary>
        /// Attempt to get the snapshot maven-metadata.xml file, which we will need to use to build up
        /// the filenames of snapshot versions.
        /// </summary>
        /// <returns>The snapshot maven-metadata.xml file if it exists, and a null result otherwise</returns>
        XmlDocument? GetSnapshotMetadata(
            MavenPackageID mavenPackageID,
            Uri feedUri,
            ICredentials feedCredentials,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            var url = feedUri.ToString().TrimEnd('/') + mavenPackageID.GroupVersionMetadataPath;
            for (var retry = 0; retry < maxDownloadAttempts; ++retry)
                try
                {
                    var request = WebRequest.Create(url);
                    request.Credentials = feedCredentials;
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.IsSuccessStatusCode() || (int)response.StatusCode == 404)
                            using (var respStream = response.GetResponseStream())
                            {
                                var xmlDoc = new XmlDocument();
                                xmlDoc.Load(respStream);
                                return xmlDoc;
                            }
                    }

                    return null;
                }
                catch (WebException ex) when (ex.Response is HttpWebResponse response &&
                    response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                catch (Exception ex)
                {
                    if (retry == maxDownloadAttempts)
                        throw new MavenDownloadException("Unable to retrieve Maven Snapshot Metadata.\r\nLast Exception Message: " + ex.Message);
                    Thread.Sleep(downloadAttemptBackoff);
                }

            return null;
        }

        //Shared code with Server. Should live in Common Location
        public string GetLatestSnapshotRelease(XmlDocument? snapshotMetadata, string? extension, string? classifier, string defaultVersion)
        {
            return snapshotMetadata?.ToEnumerable()
                    .Select(doc => doc.DocumentElement?.SelectSingleNode("./*[local-name()='versioning']"))
                    .Select(node => node?.SelectNodes("./*[local-name()='snapshotVersions']/*[local-name()='snapshotVersion']"))
                    .Where(nodes => nodes != null)
                    .SelectMany(nodes => nodes.Cast<XmlNode>())
                    .Where(node => (node.SelectSingleNode("./*[local-name()='extension']")?.InnerText.Trim() ?? "").Equals(extension?.Trim(), StringComparison.OrdinalIgnoreCase))
                    // Classifier is optional, and the XML element does not exists if the artifact has no classifier
                    .Where(node => classifier == null || (node.SelectSingleNode("./*[local-name()='classifier']")?.InnerText.Trim() ?? "").Equals(classifier.Trim(), StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(node => node.SelectSingleNode("./*[local-name()='updated']")?.InnerText)
                    .Select(node => node.SelectSingleNode("./*[local-name()='value']")?.InnerText)
                    .FirstOrDefault() ??
                defaultVersion;
        }
        
        static ICredentials GetFeedCredentials(string? feedUsername, string? feedPassword)
        {
            ICredentials credentials = CredentialCache.DefaultNetworkCredentials;
            if (!String.IsNullOrWhiteSpace(feedUsername))
            {
                credentials = new NetworkCredential(feedUsername, feedPassword);
            }
            return credentials;
        }
    }
}