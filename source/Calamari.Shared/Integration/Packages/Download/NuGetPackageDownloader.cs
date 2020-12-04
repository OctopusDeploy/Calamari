using System;
using System.IO;
using System.Linq;
using System.Net;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Packages.NuGet;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Packages.NuGet;
using Octopus.Versioning;
using PackageName = Calamari.Common.Features.Packages.PackageName;
#if USE_NUGET_V2_LIBS
using NuGet;
#else
using NuGet.Packaging;
#endif

namespace Calamari.Integration.Packages.Download
{
    public class NuGetPackageDownloader : IPackageDownloader
    {
        const string WhyAmINotAllowedToUseDependencies = "http://octopusdeploy.com/documentation/packaging";
        static readonly IPackageDownloaderUtils PackageDownloaderUtils = new PackageDownloaderUtils();

        public static readonly string DownloadingExtension = ".downloading";

        readonly ICalamariFileSystem fileSystem;
        readonly IFreeSpaceChecker freeSpaceChecker;
        readonly IVariables variables;

        public NuGetPackageDownloader(ICalamariFileSystem fileSystem, IFreeSpaceChecker freeSpaceChecker, IVariables variables)
        {
            this.fileSystem = fileSystem;
            this.freeSpaceChecker = freeSpaceChecker;
            this.variables = variables;
        }

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

            return DownloadPackage(packageId,
                version,
                feedUri,
                feedCredentials,
                cacheDirectory,
                maxDownloadAttempts,
                downloadAttemptBackoff);
        }

        PackagePhysicalFileMetadata? AttemptToGetPackageFromCache(string packageId, IVersion version, string cacheDirectory)
        {
            Log.VerboseFormat("Checking package cache for package {0} v{1}", packageId, version.ToString());

            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, PackageName.ToSearchPatterns(packageId, version, new[] { ".nupkg" }));

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

        PackagePhysicalFileMetadata DownloadPackage(
            string packageId,
            IVersion version,
            Uri feedUri,
            ICredentials feedCredentials,
            string cacheDirectory,
            int maxDownloadAttempts,
            TimeSpan downloadAttemptBackoff)
        {
            Log.Info("Downloading NuGet package {0} v{1} from feed: '{2}'", packageId, version, feedUri);
            Log.VerboseFormat("Downloaded package will be stored in: '{0}'", cacheDirectory);
            freeSpaceChecker.EnsureDiskHasEnoughFreeSpace(cacheDirectory);

            var fullPathToDownloadTo = Path.Combine(cacheDirectory, PackageName.ToCachedFileName(packageId, version, ".nupkg"));

            var downloader = new InternalNuGetPackageDownloader(fileSystem, variables);
            downloader.DownloadPackage(packageId,
                version,
                feedUri,
                feedCredentials,
                fullPathToDownloadTo,
                maxDownloadAttempts,
                downloadAttemptBackoff);

            var pkg = PackagePhysicalFileMetadata.Build(fullPathToDownloadTo);
            if (pkg == null)
                throw new CommandException($"Package metadata for {packageId}, version {version}, could not be determined");

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
                Log.Info(
                    "NuGet packages with dependencies are not currently supported, and dependencies won't be installed on the Tentacle. The package '{0} {1}' appears to have the following dependencies: {2}. For more information please see {3}",
                    pkg.PackageId,
                    pkg.Version,
                    string.Join(", ", dependencies.Select(dependency => $"{dependency.Id}")),
                    WhyAmINotAllowedToUseDependencies);
        }
    }
}