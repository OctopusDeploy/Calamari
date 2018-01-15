using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.Packages;
using Calamari.Util;
using Octopus.Versioning;
using Octopus.Versioning.Factories;
using Octopus.Versioning.Metadata;

namespace Calamari.Integration.FileSystem
{
    public class PackageStore
    {
        private readonly IPackageExtractor packageExtractorFactory;
        readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
        readonly string rootDirectory = Path.Combine(TentacleHome, "Files");
        static readonly IVersionFactory VersionFactory = new VersionFactory();

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

        public PackageStore(IPackageExtractor packageExtractorFactory)
        {
            this.packageExtractorFactory = packageExtractorFactory;
        }

        public string GetPackagesDirectory()
        {
            return rootDirectory;
        }

        public StoredPackage GetPackage(string packageFullPath)
        {
            var zip = PackageMetadata(packageFullPath);
            if (zip == null)
                return null;
            
            var package = ExtendedPackageMetadata(packageFullPath, PackageMetadata(packageFullPath));
            if (package == null)
                return null;

            return new StoredPackage(package, packageFullPath);
        }

        public StoredPackage GetPackage(PhysicalPackageMetadata metadata)
        {
            fileSystem.EnsureDirectoryExists(rootDirectory);

            foreach (var file in PackageFiles(metadata.PackageAndVersionSearchPattern))
            {
                var storedPackage = GetPackage(file);
                if (storedPackage == null)
                    continue;
                
                if (!string.Equals(storedPackage.Metadata.PackageId, metadata.PackageId, StringComparison.OrdinalIgnoreCase) || 
                    !VersionFactory.TryCreateVersion(storedPackage.Metadata.Version, out IVersion packageVersion, metadata.VersionFormat) ||
                    !packageVersion.Equals(VersionFactory.CreateVersion(metadata.Version, metadata.VersionFormat)))
                    continue;

                if (string.IsNullOrWhiteSpace(metadata.Hash))
                    return storedPackage;

                if (metadata.Hash == storedPackage.Metadata.Hash)
                    return storedPackage;
            }

            return null;
        }

        private IEnumerable<string> PackageFiles(string name)
        {
            var patterns = packageExtractorFactory.Extensions.Select(e => name + e +"-*").ToArray();
            return fileSystem.EnumerateFilesRecursively(rootDirectory, patterns);
        }

        public IEnumerable<StoredPackage> GetNearestPackages(PackageMetadata metadata, int take = 5)
        {
            if (!VersionFactory.TryCreateVersion(metadata.Version, out var version, metadata.VersionFormat))
            {
                throw new CommandException(string.Format($"Package version '{metadata.Version}' is not a valid version string"));
            }
            
            fileSystem.EnsureDirectoryExists(rootDirectory);
            var zipPackages =
                from filePath in PackageFiles(metadata.PackageSearchPattern)
                let zip = PackageMetadata(filePath)
                where zip != null && zip.PackageId == metadata.PackageId && VersionFactory.CreateVersion(zip.Version, metadata.VersionFormat).CompareTo(version) <= 0
                orderby zip.Version descending
                select new {zip, filePath};

            return
                from zipPackage in zipPackages.Take(take)
                let package = ExtendedPackageMetadata(zipPackage.filePath, zipPackage.zip)
                where package != null
                select new StoredPackage(package, zipPackage.filePath);
        }

        PackageMetadata PackageMetadata(string file)
        {
            try
            {
                return packageExtractorFactory.GetMetadata(file);
            }
            catch (Exception)
            {
                Log.Verbose($"Could not extract metadata for {file}. This file may not have a recognised filename.");
                return null;
            }
        }

        static PhysicalPackageMetadata ExtendedPackageMetadata(string file, PackageMetadata metadata)
        {
            try
            {
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    return new PhysicalPackageMetadata(metadata, 0, HashCalculator.Hash(stream));
                }
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
