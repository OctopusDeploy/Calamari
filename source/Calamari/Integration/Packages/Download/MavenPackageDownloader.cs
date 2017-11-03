using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Java;
using Calamari.Integration.Packages.NuGet;
using Calamari.Util;
using Octopus.Core.Extensions;
using Octopus.Core.Resources.Parsing.Maven;
using Octopus.Core.Resources.Versioning;
using Polly;


namespace Calamari.Integration.Packages.Download
{
    public class MavenPackageDownloader : IPackageDownloader
    {
        private static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();
        readonly CalamariPhysicalFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        
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
            throw new NotImplementedException();
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
            var mavenPackageID = new MavenPackageID(packageId, version);

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
                                            mavenPackageID.Group,
                                            mavenPackageID.Artifact,
                                            mavenPackageID.Version,
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

                
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to download package {packageId}", ex);
            }
            
            throw new NotImplementedException();
        }
    }
}