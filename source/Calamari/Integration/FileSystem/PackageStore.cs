using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Calamari.Integration.PackageDownload;
using Calamari.Integration.Packages;
using NuGet;

namespace Calamari.Integration.FileSystem
{
    public class PackageStore
    {
        readonly ICalamariFileSystem fileSystem = new CalamariPhysicalFileSystem();
        readonly string rootDirectory = Path.Combine(Environment.GetEnvironmentVariable("TentacleHome"), "Files");

        public bool DoesPackageExist(PackageMetadata metadata)
        {
            return DoesPackageExist(null, metadata);
        }

        public bool DoesPackageExist(string prefix, PackageMetadata metadata)
        {
            var package = GetPackage(prefix, metadata);
            return package != null;
        }

        public string GetFilenameForPackage(PackageMetadata metadata, string prefix = null)
        {
            var name = GetNameOfPackage(metadata);
            var fullPath = Path.Combine(GetPackageRoot(prefix), name + BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", string.Empty) + ".nupkg");

            fileSystem.EnsureDirectoryExists(rootDirectory);

            return fullPath;
        }

        public string GetPackagesDirectory()
        {
            return GetPackageRoot(null);
        }

        public string GetPackagesDirectory(string prefix)
        {
            return GetPackageRoot(prefix);
        }

        public StoredPackage GetPackage(string packageFullPath)
        {
            return ReadPackageFile(packageFullPath);
        }

        public StoredPackage GetPackage(PackageMetadata metadata)
        {
            return GetPackage(null, metadata);
        }

        public StoredPackage GetPackage(string prefix, PackageMetadata metadata)
        {
            var name = GetNameOfPackage(metadata);
            var root = GetPackageRoot(prefix);
            fileSystem.EnsureDirectoryExists(root);

            var files = fileSystem.EnumerateFilesRecursively(root, name + "*.nupkg");

            foreach (var file in files)
            {
                var package = ReadPackageFile(file);
                if (package == null)
                    continue;

                if (!string.Equals(package.Metadata.Id, metadata.Id, StringComparison.OrdinalIgnoreCase) || !string.Equals(package.Metadata.Version, metadata.Version, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(metadata.Hash))
                    return package;

                if (metadata.Hash == package.Metadata.Hash)
                    return package;
            }

            return null;
        }

        public IEnumerable<StoredPackage> GetNearestPackages(string packageId, SemanticVersion version, int take = 5)
        {
            var root = GetPackageRoot(null);
            fileSystem.EnsureDirectoryExists(root);

            var taken = 0;
            while (taken <= take)
            {
                foreach (var file in fileSystem.EnumerateFilesRecursively(root, packageId + "*.nupkg")
                    .OrderByDescending(Path.GetFileName))
                {
                    var package = GetPackage(file);
                    if (package == null)
                        continue;

                    taken++;
                    yield return package;
                }
            }
        }

        string GetPackageRoot(string prefix)
        {
            return string.IsNullOrWhiteSpace(prefix) ? rootDirectory : Path.Combine(rootDirectory, prefix);
        }

        StoredPackage ReadPackageFile(string filePath)
        {
            try
            {
                var metadata = new ZipPackage(filePath);
                string hash = HashCalculator.Hash(metadata.GetStream());

                var packageMetadata = new PackageMetadata
                {
                    Id = metadata.Id,
                    Version = metadata.Version.ToString(),
                    Hash = hash
                };

                return new StoredPackage(packageMetadata, filePath);
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

        static string GetNameOfPackage(PackageMetadata metadata)
        {
            return metadata.Id + "." + metadata.Version + "_";
        }
    }
}
