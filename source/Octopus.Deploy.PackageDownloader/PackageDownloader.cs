using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using NuGet;
using Octopus.Deploy.Startup;

namespace Octopus.Deploy.PackageDownloader
{
    class PackageDownloader
    {
        const int NumberOfTimesToAttemptToDownloadPackage = 5;
        const string WhyAmINotAllowedToUseDependencies = "http://octopusdeploy.com/documentation/packaging";
        readonly PackageRepositoryFactory packageRepositoryFactory = new PackageRepositoryFactory();

        public void DownloadPackage(string packageId, SemanticVersion version, Uri feedUri, bool forcePackageDownload, out string downloadedTo, out string hash, out long size)
        {
            Console.WriteLine("Downloading NuGet package {0} {1} from feed: '{2}'", packageId, version, feedUri);

            var cacheDirectory = Path.GetFullPath(".\\Work\\");
            OctopusLogger.VerboseFormat("Downloaded package will be stored in: {0}", cacheDirectory);
            EnsureDirectoryExists(cacheDirectory);
            //TODO: confirm enough space

            IPackage downloaded = null;
            downloadedTo = null;

            Exception downloadException = null;
            for (var i = 1; i <= NumberOfTimesToAttemptToDownloadPackage; i++)
            {
                try
                {
                    AttemptToFindAndDownloadPackage(i, packageId, version.ToString(), feedUri.ToString(), cacheDirectory, out downloaded, out downloadedTo);
                    break;
                }
                catch (Exception dataException)
                {
                    OctopusLogger.VerboseFormat("Attempt {0} of {1}: Unable to download package: {2}", i, NumberOfTimesToAttemptToDownloadPackage, dataException.Message);
                    downloadException = dataException;
                    Thread.Sleep(i * 1000);
                }
            }

            if (downloaded == null || downloadedTo == null)
            {
                if (downloadException != null)
                {
                    OctopusLogger.ErrorFormat("Unable to download package: {0}", downloadException.Message);
                }
                throw new Exception(
                    "The package could not be downloaded from NuGet. If you are getting a package verification error, try switching to a Windows File Share package repository to see if that helps.");
            }

            if (downloaded.Version != version)
            {
                throw new Exception(string.Format(
                    "Octopus requested version {0} of {1}, but the NuGet server returned a package with version {2}",
                    version, packageId, downloaded.Version));
            }

            CheckWhetherThePackageHasDependencies(downloaded);

            size = GetFileSize(downloadedTo);
            hash = HashCalculator.Hash(downloaded.GetStream());
        }

        void AttemptToFindAndDownloadPackage(int attempt, string packageId, string packageVersion, string feed, string cacheDirectory, out IPackage downloadedPackage, out string path)
        {
            NuGet.PackageDownloader downloader;
            var package = FindPackage(attempt, packageId, packageVersion, feed, out downloader);

            var fullPathToDownloadTo = GetFilePathToDownloadPackageTo(cacheDirectory, package);

            DownloadPackage(package, fullPathToDownloadTo, downloader);

            path = fullPathToDownloadTo;
            downloadedPackage = new ZipPackage(fullPathToDownloadTo);
        }

        IPackage FindPackage(int attempt, string packageId, string packageVersion, string feed, out NuGet.PackageDownloader downloader)
        {
            OctopusLogger.VerboseFormat("Finding package (attempt {0} of {1})", attempt, NumberOfTimesToAttemptToDownloadPackage);

            var remoteRepository = packageRepositoryFactory.CreateRepository(feed);

            var dspr = remoteRepository as DataServicePackageRepository;
            downloader = dspr != null ? dspr.PackageDownloader : null;

            var requiredVersion = new SemanticVersion(packageVersion);
            var package = remoteRepository.FindPackage(packageId, requiredVersion, true, true);

            if (package == null)
                throw new Exception(string.Format("Could not find package {0} {1} in feed: '{2}'", packageId, packageVersion, feed));

            if (!requiredVersion.Equals(package.Version))
            {
                var message = string.Format("The package version '{0}' returned from the package repository doesn't match the requested package version '{1}'.", package.Version, requiredVersion);
                throw new Exception(message);
            }

            return package;
        }

        string GetFilePathToDownloadPackageTo(string cacheDirectory, IPackageMetadata package)
        {
            var name = package.Id + "." + package.Version + "_" + BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty) + Constants.PackageExtension;
            return Path.Combine(cacheDirectory, name);
        }

        void DownloadPackage(IPackage package, string fullPathToDownloadTo, NuGet.PackageDownloader directDownloader)
        {
            OctopusLogger.VerboseFormat("Found package {0} version {1}", package.Id, package.Version);
            OctopusLogger.Verbose("Downloading to: " + fullPathToDownloadTo);

            var dsp = package as DataServicePackage;
            if (dsp != null && directDownloader != null)
            {
                OctopusLogger.Verbose("A direct download is possible; bypassing the NuGet machine cache");
                using (var targetFile = new FileStream(fullPathToDownloadTo, FileMode.CreateNew))
                    directDownloader.DownloadPackage(dsp.DownloadUrl, dsp, targetFile);
                return;
            }

            var physical = new PhysicalFileSystem(Path.GetDirectoryName(fullPathToDownloadTo));
            var local = new LocalPackageRepository(new FixedFilePathResolver(package.Id, fullPathToDownloadTo), physical);
            local.AddPackage(package);
        }

        long GetFileSize(string path)
        {
            return new FileInfo(path).Length;
        }

        void EnsureDirectoryExists(string cacheDirectory)
        {
            if (!Directory.Exists(cacheDirectory))
                Directory.CreateDirectory(cacheDirectory);
        }

        void CheckWhetherThePackageHasDependencies(IPackageMetadata downloaded)
        {
            var dependencies = downloaded.DependencySets.SelectMany(ds => ds.Dependencies).Count();
            if (dependencies > 0)
            {
                Console.WriteLine("NuGet packages with dependencies are not currently supported, and dependencies won't be installed on the Tentacle. The package '{0} {1}' appears to have the following dependencies: {2}. For more information please see {3}",
                               downloaded.Id,
                               downloaded.Version,
                               string.Join(", ", downloaded.DependencySets.SelectMany(ds => ds.Dependencies).Select(dependency => dependency.ToString())),
                               WhyAmINotAllowedToUseDependencies);
            }
        }
    }

    class HashCalculator
    {
        public static string Hash(Stream stream)
        {
            var hash = GetAlgorithm().ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        static HashAlgorithm GetAlgorithm()
        {
            return new SHA1CryptoServiceProvider();
        }
    }
}