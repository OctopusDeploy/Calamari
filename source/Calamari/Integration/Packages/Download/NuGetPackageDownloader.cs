using System;
using System.IO;
using System.Linq;
using System.Net;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.NuGet;
using Octopus.Versioning;
#if USE_NUGET_V2_LIBS
using NuGet;
#else
using NuGet.Packaging;

#endif


namespace Calamari.Integration.Packages.Download
{
    class NuGetPackageDownloader : IPackageDownloader
    {
        private static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();
        const string WhyAmINotAllowedToUseDependencies = "http://octopusdeploy.com/documentation/packaging";
        readonly CalamariPhysicalFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        public static readonly string DownloadingExtension = ".downloading";

        public PackagePhysicalFileMetadata DownloadPackage(
            string packageId,
            IVersion version,
            string feedId,
            Uri feedUri,
            ICredentials feedCredentials,
            bool forcePackageDownload,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            var cacheDirectory = PackageDownloaderUtils.GetPackageRoot(feedId);
            fileSystem.EnsureDirectoryExists(cacheDirectory);

            if (!forcePackageDownload)
            {
                var downloaded = AttemptToGetPackageFromCache(packageId, version, cacheDirectory);
                if (downloaded != null)
                {
                    Log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloaded.FullFilePath);
                    return downloaded;
                }
            }

            return DownloadPackage(packageId, version, feedUri, feedCredentials, cacheDirectory, maxDownloadAttempts,
                downloadAttemptBackoff);
        }

        private PackagePhysicalFileMetadata AttemptToGetPackageFromCache(string packageId, IVersion version, string cacheDirectory)
        {
            Log.VerboseFormat("Checking package cache for package {0} {1}", packageId, version.ToString());

            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version, new [] {".nupkg"}));

            foreach (var file in files)
            {
                var package = PackageName.FromFile(file);
                if (package == null)
                    continue;

                var idMatches = string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase);
                var versionExactMatch = string.Equals(package.Version.ToString(), version.ToString(), StringComparison.OrdinalIgnoreCase);
                var nugetVerMatches = package.Version.Equals(version);

                if (idMatches && (nugetVerMatches || versionExactMatch))
                {
                    return PackagePhysicalFileMetadata.Build(file, package);
                }
            }

            return null;
        }

        private PackagePhysicalFileMetadata DownloadPackage(
            string packageId,
            IVersion version,
            Uri feedUri,
            ICredentials feedCredentials,
            string cacheDirectory,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            Log.Info("Downloading NuGet package {0} {1} from feed: '{2}'", packageId, version, feedUri);
            Log.VerboseFormat("Downloaded package will be stored in: '{0}'", cacheDirectory);
            fileSystem.EnsureDiskHasEnoughFreeSpace(cacheDirectory);

            var fullPathToDownloadTo = Path.Combine(cacheDirectory, PackageName.ToNewFileName(packageId, version, ".nupkg"));

            var downloader = new InternalNuGetPackageDownloader(fileSystem);
            downloader.DownloadPackage(packageId, version, feedUri, feedCredentials, fullPathToDownloadTo, maxDownloadAttempts, downloadAttemptBackoff);

            var pkg = PackagePhysicalFileMetadata.Build(fullPathToDownloadTo);
            
            CheckWhetherThePackageHasDependencies(pkg);
            return pkg;
        }

        void CheckWhetherThePackageHasDependencies(PackagePhysicalFileMetadata pkg)
        {
            var nuGetMetadata = new LocalNuGetPackage(pkg.FullFilePath).Metadata;
#if USE_NUGET_V3_LIBS
            var dependencies = nuGetMetadata.DependencyGroups.SelectMany(ds => ds.Packages).ToArray();
#else
            var dependencies = nuGetMetadata.DependencySets.SelectMany(ds => ds.Dependencies).ToArray();
#endif
            if (dependencies.Any())
            {
                Log.Info(
                    "NuGet packages with dependencies are not currently supported, and dependencies won't be installed on the Tentacle. The package '{0} {1}' appears to have the following dependencies: {2}. For more information please see {3}",
                    pkg.PackageId,
                    pkg.Version,
                    string.Join(", ", dependencies.Select(dependency => $"{dependency.Id}")),
                    WhyAmINotAllowedToUseDependencies);
            }
        }
    }
}