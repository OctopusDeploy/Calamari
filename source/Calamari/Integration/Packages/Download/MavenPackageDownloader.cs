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

            LocalNuGetPackage downloaded = null;
            downloadedTo = null;
            if (!forcePackageDownload)
            {
                AttemptToGetPackageFromCache(
                    packageId,
                    version,
                    cacheDirectory,
                    out downloadedTo);
            }

            if (downloaded == null)
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
            string packageHash = null;
            downloaded.GetStream(stream => packageHash = HashCalculator.Hash(stream));
            hash = packageHash;
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

            var mavenPackageId = new MavenPackageID(packageId, version);

            try
            {
                /*
                 * Maven artifacts can have multiple package types. We don't know what the type
                 * is, but it has to be one of the extensions supported by the Java package
                 * extractor. So we loop over all the extensions that the Java package step
                 * supports and find the first one that is a valid artifact.
                 */
                var mavenGavFirst = JarExtractor.EXTENSIONS.AsParallel()
                                        .Select(extension => new MavenPackageID(
                                            mavenPackageId.Group,
                                            mavenPackageId.Artifact,
                                            mavenPackageId.Version,
                                            Regex.Replace(extension, "^\\.", "")))
                                        .FirstOrDefault(mavenGavParser =>
                                        {
                                            return new HttpClient().SendAsync(
                                                    new HttpRequestMessage(
                                                        HttpMethod.Head,
                                                        feedUri + mavenGavParser.ArtifactPath))
                                                .Result
                                                .IsSuccessStatusCode;
                                        }) ?? throw new Exception("Failed to find the maven artifact");

                
                downloadedTo = GetFilePathToDownloadPackageTo(
                        cacheDirectory,
                        packageId,
                        version.ToString(),
                        mavenGavFirst.Packaging);

                using (var file = fileSystem.OpenFile(downloadedTo, FileAccess.Write))
                {
                    var data = (feedUri + mavenGavFirst.ArtifactPath).ToEnumerable()
                    .Select(uri => new HttpClient().GetAsync(uri).Result)
                        .Select(result => result.Content.ReadAsByteArrayAsync().Result)
                        .First();
                    file.Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to download package {packageId}", ex);
            }
        }

        string GetFilePathToDownloadPackageTo(string cacheDirectory, string packageId, string version, string extension)
        {
            var name = packageId + "." +
                       version + "_" +
                       BitConverter.ToString(Guid.NewGuid().ToByteArray())
                           .Replace("-", string.Empty) + 
                       "." + extension;
            return Path.Combine(cacheDirectory, name);
        }
    }
}