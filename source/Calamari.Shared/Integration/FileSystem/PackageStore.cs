using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Integration.Packages;
using Octopus.Versioning;

namespace Calamari.Integration.FileSystem
{
    public class PackageStore
    {
        private readonly IPackageExtractor packageExtractorFactory;
        readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        static readonly string RootDirectory = Path.Combine(TentacleHome, "Files");

        static string TentacleHome
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

        public PackageStore(IPackageExtractor packageExtractorFactory)
        {
            this.packageExtractorFactory = packageExtractorFactory;
        }

        public static string GetPackagesDirectory()
        {
            return RootDirectory;
        }

        public PackagePhysicalFileMetadata GetPackage(string packageId, IVersion version, string hash)
        {
            fileSystem.EnsureDirectoryExists(RootDirectory);
            foreach (var file in PackageFiles(packageId, version))
            {
                var packageNameMetadata = PackageMetadata(file);
                if (packageNameMetadata == null)
                    continue;
                
                if (!string.Equals(packageNameMetadata.PackageId, packageId, StringComparison.OrdinalIgnoreCase) || 
                    !packageNameMetadata.Version.Equals(version))
                    continue;

                var physicalPackageMetadata = PackagePhysicalFileMetadata.Build(file, packageNameMetadata);

                if (string.IsNullOrWhiteSpace(hash) || hash == physicalPackageMetadata.Hash)
                    return physicalPackageMetadata;
            }

            return null;
        }

        private IEnumerable<string> PackageFiles(string packageId, IVersion version = null)
        {
            return fileSystem.EnumerateFilesRecursively(RootDirectory, 
                PackageName.ToSearchPatterns(packageId, version, packageExtractorFactory.Extensions));
        }

        public IEnumerable<PackagePhysicalFileMetadata> GetNearestPackages(string packageId, IVersion version, int take = 5)
        {
            fileSystem.EnsureDirectoryExists(RootDirectory);

            var zipPackages =
                from filePath in PackageFiles(packageId)
                let zip = PackageMetadata(filePath)
                where zip != null && string.Equals(zip.PackageId, packageId, StringComparison.OrdinalIgnoreCase) && zip.Version.CompareTo(version) <= 0
                orderby zip.Version descending
                select new {zip, filePath};

            return
                from zipPackage in zipPackages.Take(take)
                let package = PackagePhysicalFileMetadata.Build(zipPackage.filePath, zipPackage.zip)
                where package != null
                select package;
        }

        PackageFileNameMetadata PackageMetadata(string file)
        {
            try
            {
                return PackageName.FromFile(file);
            }
            catch (Exception)
            {
                Log.Verbose($"Could not extract metadata for {file}. This file may be corrupt or not have a recognised filename.");
                return null;
            }
        }
    }
}
