#if USE_NUGET_V2_LIBS
using System;
using System.IO;
using System.Net;
using Calamari.Integration.Packages.Download;
using NuGet;
using SemanticVersion = global::NuGet.SemanticVersion;

namespace Calamari.Integration.Packages.NuGet
{
    public class NuGetV2Downloader
    {
        public static void DownloadPackage(string packageId, string packageVersion, Uri feedUri,
            ICredentials feedCredentials, string targetFilePath)
        {
            SetFeedCredentials(feedUri, feedCredentials);

            global::NuGet.PackageDownloader downloader;
            var package = FindPackage(packageId, packageVersion, feedUri, out downloader);
            DownloadPackage(package, targetFilePath, downloader);
        }

        static IPackage FindPackage(string packageId, string packageVersion, Uri feed,
            out global::NuGet.PackageDownloader downloader)
        {
            var remoteRepository = PackageRepositoryFactory.Default.CreateRepository(feed.AbsoluteUri);

            var dspr = remoteRepository as DataServicePackageRepository;
            downloader = dspr != null ? dspr.PackageDownloader : null;

            var requiredVersion = new SemanticVersion(packageVersion);
            var package = remoteRepository.FindPackage(packageId, requiredVersion, true, true);

            if (package == null)
                throw new Exception(string.Format("Could not find package {0} {1} in feed: '{2}'", packageId,
                    packageVersion, feed));

            if (!requiredVersion.Equals(package.Version))
            {
                var message =
                    string.Format(
                        "The package version '{0}' returned from the package repository doesn't match the requested package version '{1}'.",
                        package.Version, requiredVersion);
                throw new Exception(message);
            }

            return package;
        }

        static void DownloadPackage(IPackage package, string fullPathToDownloadTo,
            global::NuGet.PackageDownloader directDownloader)
        {
            Log.VerboseFormat("Found package {0} version {1}", package.Id, package.Version);
            Log.Verbose("Downloading to: " + fullPathToDownloadTo);

            var dsp = package as DataServicePackage;
            if (dsp != null && directDownloader != null)
            {
                Log.Verbose("A direct download is possible; bypassing the NuGet machine cache");
                using (var targetFile = new FileStream(fullPathToDownloadTo, FileMode.CreateNew))
                    directDownloader.DownloadPackage(dsp.DownloadUrl, dsp, targetFile);
                return;
            }

            var physical = new PhysicalFileSystem(Path.GetDirectoryName(fullPathToDownloadTo));
            var local = new LocalPackageRepository(new FixedFilePathResolver(package.Id, fullPathToDownloadTo), physical);
            local.AddPackage(package);
        }

        static void SetFeedCredentials(Uri feedUri, ICredentials feedCredentials)
        {
            FeedCredentialsProvider.Instance.SetCredentials(feedUri, feedCredentials);
            HttpClient.DefaultCredentialProvider = FeedCredentialsProvider.Instance;
        }
    }
}
#endif