#if USE_NUGET_V2_LIBS
using System;
using System.IO;
using System.Net;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Packages.Download;
using NuGet;
using SemanticVersion = NuGet.SemanticVersion;

namespace Calamari.Integration.Packages.NuGet
{
    public class NuGetV2Downloader
    {
        public static void DownloadPackage(string packageId, string packageVersion, Uri feedUri,
            ICredentials feedCredentials, string targetFilePath)
        {
            SetFeedCredentials(feedUri, feedCredentials);

            var package = FindPackage(packageId, packageVersion, feedUri, out var downloader);
            DownloadPackage(package, targetFilePath, downloader);
        }

        private static IPackage FindPackage(string packageId, string packageVersion, Uri feed, out PackageDownloader downloader)
        {
            var remoteRepository = PackageRepositoryFactory.Default.CreateRepository(feed.AbsoluteUri);

            downloader = remoteRepository is DataServicePackageRepository dspr ? dspr.PackageDownloader : null;

            var requiredVersion = new SemanticVersion(packageVersion);
            var package = remoteRepository.FindPackage(packageId, requiredVersion, true, true);

            if (package == null)
                throw new Exception($"Could not find package {packageId} {packageVersion} in feed: '{feed}'");

            if (!requiredVersion.Equals(package.Version))
            {
                throw new Exception($"The package version '{package.Version}' returned from the package repository doesn't match the requested package version '{requiredVersion}'.");
            }

            return package;
        }

        private static void DownloadPackage(IPackage package, string fullPathToDownloadTo, PackageDownloader directDownloader)
        {
            Log.VerboseFormat("Found package {0} v{1}", package.Id, package.Version);
            Log.Verbose("Downloading to: " + fullPathToDownloadTo);

            if (package is DataServicePackage dsp && directDownloader != null)
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