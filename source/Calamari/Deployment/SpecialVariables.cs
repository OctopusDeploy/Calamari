namespace Calamari.Deployment
{
    public static class SpecialVariables
    {
        public const string LastErrorMessage = "OctopusLastErrorMessage";
        public const string LastError = "OctopusLastError";

        public static readonly string AppliedXmlConfigTransforms = "OctopusAppliedXmlConfigTransforms";

        public static string GetLibraryScriptModuleName(string variableName)
        {
            return variableName.Replace("Octopus.Script.Module[", "").TrimEnd(']');
        }

        public static bool IsExcludedFromLocalVariables(string name)
        {
            return name.Contains("[");
        }

        public static bool IsLibraryScriptModule(string variableName)
        {
            return variableName.StartsWith("Octopus.Script.Module[");
        }

        public const string OriginalPackageDirectoryPath = "OctopusOriginalPackageDirectoryPath";

        public static class Tentacle
        {
            public static class CurrentDeployment
            {
                public static readonly string PackageFilePath = "Octopus.Tentacle.CurrentDeployment.PackageFilePath";
                public static readonly string RetentionPolicySubset = "Octopus.Tentacle.CurrentDeployment.RetentionPolicySubset";
                public static readonly string TargetedRoles = "Octopus.Tentacle.CurrentDeployment.TargetedRoles";
            }

            public static class PreviousInstallation
            {
                public static readonly string PackageVersion = "Octopus.Tentacle.PreviousInstallation.PackageVersion";
                public static readonly string PackageFilePath = "Octopus.Tentacle.PreviousInstallation.PackageFilePath";
                public static readonly string OriginalInstalledPath = "Octopus.Tentacle.PreviousInstallation.OriginalInstalledPath";
                public static readonly string CustomInstallationDirectory = "Octopus.Tentacle.PreviousInstallation.CustomInstallationDirectory";
            }

            public static class Agent
            {
                public static readonly string ApplicationDirectoryPath = "Octopus.Tentacle.Agent.ApplicationDirectoryPath";
                public static readonly string EnvironmentApplicationDirectoryPath = "env:Octopus.Tentacle.Agent.ApplicationDirectoryPath";
                public static readonly string InstanceName = "Octopus.Tentacle.Agent.InstanceName";
                public static readonly string ProgramDirectoryPath = "Octopus.Tentacle.Agent.ProgramDirectoryPath";
            }
        }

        public static class Package
        {
            public static readonly string NuGetPackageId = "Octopus.Action.Package.NuGetPackageId";
            public static readonly string NuGetPackageVersion = "Octopus.Action.Package.NuGetPackageVersion";
            public static readonly string ShouldDownloadOnTentacle = "Octopus.Action.Package.DownloadOnTentacle";
            public static readonly string NuGetFeedId = "Octopus.Action.Package.NuGetFeedId";
            public static readonly string UpdateIisWebsite = "Octopus.Action.Package.UpdateIisWebsite";
            public static readonly string UpdateIisWebsiteName = "Octopus.Action.Package.UpdateIisWebsiteName";
            public static readonly string CustomInstallationDirectory = "Octopus.Action.Package.CustomInstallationDirectory";
            public static readonly string CustomInstallationDirectoryShouldBePurgedBeforeDeployment = "Octopus.Action.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment";
            public static readonly string AutomaticallyUpdateAppSettingsAndConnectionStrings = "Octopus.Action.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings";
            public static readonly string AutomaticallyRunConfigurationTransformationFiles = "Octopus.Action.Package.AutomaticallyRunConfigurationTransformationFiles";
            public static readonly string IgnoreConfigTranformationErrors = "Octopus.Action.Package.IgnoreConfigTranformationErrors";
            public static readonly string AdditionalXmlConfigurationTransforms = "Octopus.Action.Package.AdditionalXmlConfigurationTransforms";
            public static readonly string SubstituteInFilesEnabled = "Octopus.Action.SubstituteInFiles.Enabled";
            public static readonly string SubstituteInFilesTargets = "Octopus.Action.SubstituteInFiles.TargetFiles";
        }

        public static class Environment
        {
            public static readonly string Id = "Octopus.Environment.Id";
            public static readonly string Name = "Octopus.Environment.Name";
        }
    }
}