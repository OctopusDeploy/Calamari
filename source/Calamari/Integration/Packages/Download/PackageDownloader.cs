using System;
using System.IO;
using System.Linq;
using System.Net;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.NuGet;
using Calamari.Util;
using NuGet;
using NuGet.Versioning;

namespace Calamari.Integration.Packages.Download
{
    class PackageDownloader
    {
        const string WhyAmINotAllowedToUseDependencies = "http://octopusdeploy.com/documentation/packaging";
        readonly CalamariPhysicalFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        readonly string rootDirectory = Path.Combine(TentacleHome, "Files");


        private static string TentacleHome
        {
            get
            {
                var tentacleHome = Environment.GetEnvironmentVariable("TentacleHome");
                if (tentacleHome == null)
                {
                    Log.Error("Environment variable 'TentacleHome' has not been set.");
                }
                return tentacleHome;
            }
        }

        public void DownloadPackage(string packageId, NuGetVersion version, string feedId, Uri feedUri, ICredentials feedCredentials, bool forcePackageDownload, out string downloadedTo, out string hash, out long size)
        {
            var cacheDirectory = GetPackageRoot(feedId);
            
            IPackage downloaded = null;
            downloadedTo = null;
            if (!forcePackageDownload)
            {
                AttemptToGetPackageFromCache(packageId, version, feedId, cacheDirectory, out downloaded, out downloadedTo);
            }

            if (downloaded == null)
            {
                DownloadPackage(packageId, version, feedUri, feedCredentials, cacheDirectory, out downloaded, out downloadedTo);
            }
            else
            {
                Log.VerboseFormat("Package was found in cache. No need to download. Using file: '{0}'", downloadedTo);
            }

            size = fileSystem.GetFileSize(downloadedTo);
            hash = HashCalculator.Hash(downloaded.GetStream());
        }

        private void AttemptToGetPackageFromCache(string packageId, NuGetVersion version, string feedId, string cacheDirectory, out IPackage downloaded, out string downloadedTo)
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

        private string GetPackageRoot(string prefix)
        {
            return string.IsNullOrWhiteSpace(prefix) ? rootDirectory : Path.Combine(rootDirectory, prefix);
        }

        private string GetNameOfPackage(string packageId, string version)
        {
            return String.Format("{0}.{1}_", packageId, version);
        }

        private void DownloadPackage(string packageId, NuGetVersion version, Uri feedUri, ICredentials feedCredentials, string cacheDirectory, out IPackage downloaded, out string downloadedTo)
        {
            Log.Info("Downloading NuGet package {0} {1} from feed: '{2}'", packageId, version, feedUri);
            Log.VerboseFormat("Downloaded package will be stored in: '{0}'", cacheDirectory);
            fileSystem.EnsureDirectoryExists(cacheDirectory);
            fileSystem.EnsureDiskHasEnoughFreeSpace(cacheDirectory);

            var fullPathToDownloadTo = GetFilePathToDownloadPackageTo(cacheDirectory, packageId, version.ToString());

           NuGetPackageDownloader.DownloadPackage(packageId, version, feedUri, feedCredentials, fullPathToDownloadTo); 

            downloaded = new ZipPackage(fullPathToDownloadTo);
            downloadedTo = fullPathToDownloadTo; 
            CheckWhetherThePackageHasDependencies(downloaded);
        }


        string GetFilePathToDownloadPackageTo(string cacheDirectory, string packageId, string version)
        {
            var name = packageId + "." + version + "_" + BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty) + Constants.PackageExtension;
            return Path.Combine(cacheDirectory, name);
        }

        void CheckWhetherThePackageHasDependencies(IPackageMetadata downloaded)
        {
            var dependencies = downloaded.DependencySets.SelectMany(ds => ds.Dependencies).Count();
            if (dependencies > 0)
            {
                Log.Info("NuGet packages with dependencies are not currently supported, and dependencies won't be installed on the Tentacle. The package '{0} {1}' appears to have the following dependencies: {2}. For more information please see {3}",
                               downloaded.Id,
                               downloaded.Version,
                               string.Join(", ", downloaded.DependencySets.SelectMany(ds => ds.Dependencies).Select(dependency => dependency.ToString())),
                               WhyAmINotAllowedToUseDependencies);
            }
        }
    }
}