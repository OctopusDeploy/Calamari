using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Packages.Java;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Packages;
using Octopus.Versioning;

namespace Calamari.Integration.FileSystem
{
    public interface IPackageStore
    {
        PackagePhysicalFileMetadata? GetPackage(string packageId, IVersion version, string hash);
        IEnumerable<PackagePhysicalFileMetadata> GetNearestPackages(string packageId, IVersion version, int take = 5);
    }

    public class PackageStore : IPackageStore
    {
        readonly ICalamariFileSystem fileSystem;
        readonly string[] supportedExtensions;

        public PackageStore(ICombinedPackageExtractor packageExtractor, ICalamariFileSystem fileSystem)
        {
            this.supportedExtensions = packageExtractor.Extensions.Concat(JarPackageExtractor.SupportedExtensions).Distinct().ToArray();
            this.fileSystem = fileSystem;
        }

        public static string GetPackagesDirectory()
        {
            var tentacleHome = Environment.GetEnvironmentVariable("TentacleHome");
            if (tentacleHome == null)
                throw new Exception("Environment variable 'TentacleHome' has not been set.");
            
            return Path.Combine(tentacleHome, "Files");
        }

        public PackagePhysicalFileMetadata? GetPackage(string packageId, IVersion version, string hash)
        {
            fileSystem.EnsureDirectoryExists(GetPackagesDirectory());
            foreach (var file in PackageFiles(packageId, version))
            {
                var packageNameMetadata = PackageMetadata(file);
                if (packageNameMetadata == null)
                    continue;

                if (!string.Equals(packageNameMetadata.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
                    continue;
                
                if (!packageNameMetadata.Version.Equals(version) && !packageNameMetadata.FileVersion.Equals(version))
                    continue;

                var physicalPackageMetadata = PackagePhysicalFileMetadata.Build(file, packageNameMetadata);

                if (string.IsNullOrWhiteSpace(hash) || hash == physicalPackageMetadata?.Hash)
                    return physicalPackageMetadata;
            }

            return null;
        }

        IEnumerable<string> PackageFiles(string packageId, IVersion? version = null)
        {
            return fileSystem.EnumerateFilesRecursively(GetPackagesDirectory(),
                PackageName.ToSearchPatterns(packageId, version, supportedExtensions));
        }

        public IEnumerable<PackagePhysicalFileMetadata> GetNearestPackages(string packageId, IVersion version, int take = 5)
        {
            fileSystem.EnsureDirectoryExists(GetPackagesDirectory());

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

        PackageFileNameMetadata? PackageMetadata(string file)
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