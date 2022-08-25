using System;

namespace Calamari.Common.Plumbing.Variables
{
    public static class PackageVariables
    {
        public static readonly string TransferPath = "Octopus.Action.Package.TransferPath";
        public static readonly string OriginalFileName = "Octopus.Action.Package.OriginalFileName";
        public static readonly string CustomInstallationDirectory = "Octopus.Action.Package.CustomInstallationDirectory";
        public static readonly string CustomPackageFileName = "Octopus.Action.Package.CustomPackageFileName";
        public static readonly string JavaArchiveCompression = "Octopus.Action.Package.JavaArchiveCompression";
        public static readonly string CustomInstallationDirectoryShouldBePurgedBeforeDeployment = "Octopus.Action.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment";
        public static readonly string CustomInstallationDirectoryPurgeExclusions = "Octopus.Action.Package.CustomInstallationDirectoryPurgeExclusions";
        public static readonly string EnableNoMatchWarning = "Octopus.Action.SubstituteInFiles.EnableNoMatchWarning";
        public static readonly string SubstituteInFilesOutputEncoding = "Octopus.Action.SubstituteInFiles.OutputEncoding";
        public static readonly string SubstituteInFilesTargets = "Octopus.Action.SubstituteInFiles.TargetFiles";
        public static readonly string PackageCollection = "Octopus.Action.Package";

        public static string PackageId => IndexedPackageId(string.Empty);
        public static string PackageVersion => IndexedPackageVersion(string.Empty);

        public static string IndexedPackageId(string packageReferenceName) => $"Octopus.Action.Package[{packageReferenceName}].PackageId";

        public static string IndexedPackageVersion(string packageReferenceName) => $"Octopus.Action.Package[{packageReferenceName}].PackageVersion";

        public static string IndexedOriginalPath(string packageReferenceName)
        {
            return $"Octopus.Action.Package[{packageReferenceName}].OriginalPath";
        }

        public static string IndexedExtract(string packageReferenceName)
        {
            return $"Octopus.Action.Package[{packageReferenceName}].Extract";
        }

        public class Output
        {
            public static readonly string DeprecatedInstallationDirectoryPath = "Package.InstallationDirectoryPath";
            public static readonly string InstallationDirectoryPath = "Octopus.Action.Package.InstallationDirectoryPath";
            public static readonly string InstallationPackagePath = "Octopus.Action.Package.InstallationPackagePath";
            public static readonly string ExtractedFileCount = "Package.ExtractedFileCount";
            public static readonly string CopiedFileCount = "Package.CopiedFileCount";
            public static readonly string DirectoryPath = "Package.DirectoryPath";
            public static readonly string FileName = "Package.FileName";
            public static readonly string FilePath = "Package.FilePath";
        }
    }
}