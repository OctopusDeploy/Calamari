using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Calamari.Integration.FileSystem;
using NuGet;

namespace Calamari.Integration.PackageDownload
{
    class PackageDownloader
    {
        const int NumberOfTimesToAttemptToDownloadPackage = 5;
        const string WhyAmINotAllowedToUseDependencies = "http://octopusdeploy.com/documentation/packaging";
        readonly PackageRepositoryFactory packageRepositoryFactory = new PackageRepositoryFactory();
        readonly CalamariPhysicalFileSystem fileSystem = new CalamariPhysicalFileSystem();

        public void DownloadPackage(string packageId, SemanticVersion version, string feedId, Uri feedUri, bool forcePackageDownload, out string downloadedTo, out string hash, out long size)
        {
            var cacheDirectory = GetPackageRoot(feedId, Path.GetFullPath(".\\Work\\"));
            
            IPackage downloaded = null;
            downloadedTo = null;
            if (!forcePackageDownload)
            {
                AttemptToGetPackageFromCache(packageId, version, feedId, cacheDirectory, out downloaded, out downloadedTo);
            }

            if (downloaded == null)
            {
                AttemptToDownload(packageId, version, feedUri, cacheDirectory, out downloadedTo, out downloaded);
            }
            else
            {
                Log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloadedTo);
            }

            size = fileSystem.GetFileSize(downloadedTo);
            hash = HashCalculator.Hash(downloaded.GetStream());
        }

        private void AttemptToGetPackageFromCache(string packageId, SemanticVersion version, string feedId, string cacheDirectory, out IPackage downloaded, out string downloadedTo)
        {
            downloaded = null;
            downloadedTo = null;

            Log.VerboseFormat("Checking package cache for package {0} {1}", packageId, version.ToString());

            var name = GetNameOfPackage(packageId, version.ToString());
            fileSystem.EnsureDirectoryExists(cacheDirectory);

            var files = fileSystem.EnumerateFilesRecursively(cacheDirectory, name + "*.nupkg");

            foreach (var file in files)
            {
                var package = ReadPackageFile(file);
                if (package == null)
                    continue;

                if (!string.Equals(package.Id, packageId, StringComparison.OrdinalIgnoreCase) || !string.Equals(package.Version.ToString(), version.ToString(), StringComparison.OrdinalIgnoreCase))
                    continue;

                downloaded = package;
                downloadedTo = file;
            }
        }

        private IPackage ReadPackageFile(string filePath)
        {
            try
            {
                return new ZipPackage(filePath);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (FileFormatException)
            {
                return null;
            }
        }

        private string GetPackageRoot(string prefix, string rootDirectory)
        {
            return string.IsNullOrWhiteSpace(prefix) ? rootDirectory : Path.Combine(rootDirectory, prefix);
        }

        private string GetNameOfPackage(string packageId, string version)
        {
            return String.Format("{0}.{1}_", packageId, version);
        }

        private void AttemptToDownload(string packageId, SemanticVersion version, Uri feedUri, string cacheDirectory, out string downloadedTo, out IPackage downloaded)
        {
            Console.WriteLine("Downloading NuGet package {0} {1} from feed: '{2}'", packageId, version, feedUri);

            Log.VerboseFormat("Downloaded package will be stored in: '{0}'", cacheDirectory);
            fileSystem.EnsureDirectoryExists(cacheDirectory);
            fileSystem.EnsureDiskHasEnoughFreeSpace(cacheDirectory);

            downloaded = null;
            downloadedTo = null;
            Exception downloadException = null;
            for (var i = 1; i <= NumberOfTimesToAttemptToDownloadPackage; i++)
            {
                try
                {
                    AttemptToFindAndDownloadPackage(i, packageId, version.ToString(), feedUri.ToString(), cacheDirectory,
                        out downloaded, out downloadedTo);
                    break;
                }
                catch (Exception dataException)
                {
                    Log.VerboseFormat("Attempt {0} of {1}: Unable to download package: {2}", i,
                        NumberOfTimesToAttemptToDownloadPackage, dataException.Message);
                    downloadException = dataException;
                    Thread.Sleep(i*1000);
                }
            }

            if (downloaded == null || downloadedTo == null)
            {
                if (downloadException != null)
                {
                    Log.ErrorFormat("Unable to download package: {0}", downloadException.Message);
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
            Log.VerboseFormat("Finding package (attempt {0} of {1})", attempt, NumberOfTimesToAttemptToDownloadPackage);

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