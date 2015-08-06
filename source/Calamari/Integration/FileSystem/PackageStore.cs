using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Integration.Packages;
using Calamari.Util;
using NuGet;

namespace Calamari.Integration.FileSystem
{
    public class PackageStore
    {
        readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
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

        public string GetPackagesDirectory()
        {
            return rootDirectory;
        }

        public StoredPackage GetPackage(string packageFullPath)
        {
            var zip = ReadZipPackage(packageFullPath);
            if (zip == null)
                return null;
            
            var package = ReadPackageFile(new ZipPackage(packageFullPath));
            if (package == null)
                return null;

            return new StoredPackage(package, packageFullPath);
        }

        public StoredPackage GetPackage(PackageMetadata metadata)
        {
            var name = GetNameOfPackage(metadata);
            fileSystem.EnsureDirectoryExists(rootDirectory);

            var files = fileSystem.EnumerateFilesRecursively(rootDirectory, name + ".nupkg-*");

            foreach (var file in files)
            {
                var storedPackage = GetPackage(file);
                if (storedPackage == null)
                    continue;

                if (!string.Equals(storedPackage.Metadata.Id, metadata.Id, StringComparison.OrdinalIgnoreCase) || !string.Equals(storedPackage.Metadata.Version, metadata.Version, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(metadata.Hash))
                    return storedPackage;

                if (metadata.Hash == storedPackage.Metadata.Hash)
                    return storedPackage;
            }

            return null;
        }

        public IEnumerable<StoredPackage> GetNearestPackages(string packageId, SemanticVersion version, int take = 5)
        {
            fileSystem.EnsureDirectoryExists(rootDirectory);

            var zipPackages =
                from filePath in fileSystem.EnumerateFilesRecursively(rootDirectory, packageId + "*.nupkg-*")
                let zip = ReadZipPackage(filePath)
                where zip != null && zip.Id == packageId && zip.Version < version
                orderby zip.Version descending
                select new {zip, filePath};

            return
                from zipPackage in zipPackages.Take(take)
                let package = ReadPackageFile(zipPackage.zip)
                where package != null
                select new StoredPackage(package, zipPackage.filePath);
        }

        static ZipPackage ReadZipPackage(string file)
        {
            try
            {
                return new ZipPackage(file);
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

        static PackageMetadata ReadPackageFile(IPackage zip)
        {
            try
            {
                using (var zipStream = zip.GetStream())
                {
                   return new PackageMetadata
                    {
                        Id = zip.Id,
                        Version = zip.Version.ToString(),
                        Hash = HashCalculator.Hash(zipStream),
                    };
                }
            }
            catch (IOException)
            {
                return null;
            }
        }

        static string GetNameOfPackage(PackageMetadata metadata)
        {
            return metadata.Id + "." + metadata.Version;
        }
    }
}
