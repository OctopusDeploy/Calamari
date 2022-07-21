#if USE_NUGET_V2_LIBS
using System;
using System.IO;
using NuGet;

namespace Calamari.Integration.Packages.Download
{
    public class FixedFilePathResolver : IPackagePathResolver
    {
        readonly string packageName;
        readonly string filePathNameToReturn;

        public FixedFilePathResolver(string packageName, string filePathNameToReturn)
        {
            if (packageName == null)
                throw new ArgumentNullException("packageName");
            if (filePathNameToReturn == null)
                throw new ArgumentNullException("filePathNameToReturn");

            this.packageName = packageName;
            this.filePathNameToReturn = filePathNameToReturn;
        }

        public string GetInstallPath(IPackage package)
        {
            EnsureRightPackage(package.Id);
            return Path.GetDirectoryName(filePathNameToReturn);
        }

        public string GetPackageDirectory(IPackage package)
        {
            return GetPackageDirectory(package.Id, package.Version);
        }

        public string GetPackageFileName(IPackage package)
        {
            return GetPackageFileName(package.Id, package.Version);
        }

        public string GetPackageDirectory(string packageId, SemanticVersion version)
        {
            EnsureRightPackage(packageId);
            return string.Empty;
        }

        public string GetPackageFileName(string packageId, SemanticVersion version)
        {
            EnsureRightPackage(packageId);
            return Path.GetFileName(filePathNameToReturn);
        }

        void EnsureRightPackage(string packageId)
        {
            var samePackage = string.Equals(packageId, packageName, StringComparison.OrdinalIgnoreCase);

            if (!samePackage)
            {
                throw new ArgumentException(string.Format("Expected to be asked for the path for package {0}, but was instead asked for the path for package {1}", packageName, packageId));
            }
        }
    }
}
#endif