namespace Calamari.Common.Variables
{
    public static class PackageVariables
    {
        
        public static readonly string TransferPath = "Octopus.Action.Package.TransferPath";
        public static readonly string PackageId = "Octopus.Action.Package.PackageId";
        public static readonly string PackageVersion = "Octopus.Action.Package.PackageVersion";
        public static readonly string OriginalFileName = "Octopus.Action.Package.OriginalFileName";
        public static readonly string CustomInstallationDirectory = "Octopus.Action.Package.CustomInstallationDirectory";
        public static readonly string CustomPackageFileName = "Octopus.Action.Package.CustomPackageFileName";
        public static readonly string CustomInstallationDirectoryShouldBePurgedBeforeDeployment = "Octopus.Action.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment";
        public static readonly string CustomInstallationDirectoryPurgeExclusions = "Octopus.Action.Package.CustomInstallationDirectoryPurgeExclusions";
        public static readonly string EnableNoMatchWarning = "Octopus.Action.SubstituteInFiles.EnableNoMatchWarning";
        public static readonly string SubstituteInFilesOutputEncoding = "Octopus.Action.SubstituteInFiles.OutputEncoding";
        public static readonly string SubstituteInFilesEnabled = "Octopus.Action.SubstituteInFiles.Enabled";
        public static readonly string SubstituteInFilesTargets = "Octopus.Action.SubstituteInFiles.TargetFiles";
        public static readonly string PackageCollection = "Octopus.Action.Package";
            
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
        
        public static string PackageIdWithKey(string key)
        {
            return $"Octopus.Action.Package[{key}].PackageId";
        }

        public static string PackageVersionWithKey(string key)
        {
            return $"Octopus.Action.Package[{key}].PackageVersion";
        }
        
        public static string OriginalPathWithKey(string key)
        {
            return $"Octopus.Action.Package[{key}].OriginalPath";
        }
        
    }
}