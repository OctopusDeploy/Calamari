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

        public static string GetOutputVariableName(string actionName, string variableName)
        {
            return string.Format("Octopus.Action[{0}].Output.{1}", actionName, variableName);
        }

        public static string GetMachineIndexedOutputVariableName(string actionName, string machineName, string variableName)
        {
            return string.Format("Octopus.Action[{0}].Output[{1}].{2}", actionName, machineName, variableName);
        }

        public const string OriginalPackageDirectoryPath = "OctopusOriginalPackageDirectoryPath";
        public const string UseLegacyIisSupport = "OctopusUseLegacyIisSupport";

        public static readonly string RetentionPolicySet = "OctopusRetentionPolicySet";
        public static readonly string RetentionPolicyItemsToKeep = "OctopusRetentionPolicyItemsToKeep";
        public static readonly string PrintVariables = "OctopusPrintVariables";
        public static readonly string RetentionPolicyDaysToKeep = "OctopusRetentionPolicyDaysToKeep";
        public static readonly string PrintEvaluatedVariables = "OctopusPrintEvaluatedVariables";

        public static readonly string SkipFreeDiskSpaceCheck = "OctopusSkipFreeDiskSpaceCheck";
        public static readonly string FreeDiskSpaceOverrideInMegaBytes = "OctopusFreeDiskSpaceOverrideInMegaBytes";

        public static readonly string DeleteScriptsOnCleanup = "OctopusDeleteScriptsOnCleanup";

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
                public static readonly string InstanceName = "Octopus.Tentacle.Agent.InstanceName";
                public static readonly string ProgramDirectoryPath = "Octopus.Tentacle.Agent.ProgramDirectoryPath";
                public static readonly string JournalPath = "env:TentacleJournal";
            }
        }

        public static class Deployment
        {
            public static class Tenant
            {
                public static string Id = "Octopus.Deployment.Tenant.Id";
                public static string Name = "Octopus.Deployment.Tenant.Name";
            }
        }

        public static class Package
        {
            public static readonly string TransferPath = "Octopus.Action.Package.TransferPath";
            public static readonly string NuGetPackageId = "Octopus.Action.Package.NuGetPackageId";
            public static readonly string NuGetPackageVersion = "Octopus.Action.Package.NuGetPackageVersion";
            public static readonly string ShouldDownloadOnTentacle = "Octopus.Action.Package.DownloadOnTentacle";
            public static readonly string NuGetFeedId = "Octopus.Action.Package.NuGetFeedId";
            public static readonly string OriginalFileName = "Octopus.Action.Package.OriginalFileName";
            public static readonly string EnabledFeatures = "Octopus.Action.EnabledFeatures";
            public static readonly string UpdateIisWebsite = "Octopus.Action.Package.UpdateIisWebsite";
            public static readonly string UpdateIisWebsiteName = "Octopus.Action.Package.UpdateIisWebsiteName";
            public static readonly string CustomInstallationDirectory = "Octopus.Action.Package.CustomInstallationDirectory";
            public static readonly string CustomInstallationDirectoryShouldBePurgedBeforeDeployment = "Octopus.Action.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment";
            public static readonly string AutomaticallyUpdateAppSettingsAndConnectionStrings = "Octopus.Action.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings";
            public static readonly string JsonConfigurationVariablesEnabled = "Octopus.Action.Package.JsonConfigurationVariablesEnabled";
            public static readonly string JsonConfigurationVariablesTargets = "Octopus.Action.Package.JsonConfigurationVariablesTargets";
            public static readonly string AutomaticallyRunConfigurationTransformationFiles = "Octopus.Action.Package.AutomaticallyRunConfigurationTransformationFiles";
            public static readonly string IgnoreConfigTransformationErrors = "Octopus.Action.Package.IgnoreConfigTransformationErrors";
            public static readonly string SuppressConfigTransformationLogging = "Octopus.Action.Package.SuppressConfigTransformationLogging";
            public static readonly string EnableDiagnosticsConfigTransformationLogging = "Octopus.Action.Package.EnableDiagnosticsConfigTransformationLogging";
            public static readonly string AdditionalXmlConfigurationTransforms = "Octopus.Action.Package.AdditionalXmlConfigurationTransforms";
            public static readonly string SubstituteInFilesEnabled = "Octopus.Action.SubstituteInFiles.Enabled";
            public static readonly string SubstituteInFilesTargets = "Octopus.Action.SubstituteInFiles.TargetFiles";
            public static readonly string SubstituteInFilesOutputEncoding = "Octopus.Action.SubstituteInFiles.OutputEncoding";
            public static readonly string SkipIfAlreadyInstalled = "Octopus.Action.Package.SkipIfAlreadyInstalled";
            public static readonly string IgnoreVariableReplacementErrors = "Octopus.Action.Package.IgnoreVariableReplacementErrors";
            public static readonly string SuppressNestedScriptWarning = "OctopusSuppressNestedScriptWarning"; 

            public class Output
            {
                public static readonly string DeprecatedInstallationDirectoryPath = "Package.InstallationDirectoryPath";
                public static readonly string InstallationDirectoryPath = "Octopus.Action.Package.InstallationDirectoryPath";
                public static readonly string DirectoryPath = "Package.DirectoryPath";
                public static readonly string FileName = "Package.FileName";
                public static readonly string FilePath = "Package.FilePath";
            }
        }

        public static class Vhd
        {
            public static readonly string ApplicationPath = "Octopus.Action.Vhd.ApplicationPath";
            public static readonly string VmName = "Octopus.Action.Vhd.VmName";
            public static readonly string DeployVhdToVm = "Octopus.Action.Vhd.DeployVhdToVm";
        }

        public static class Environment
        {
            public static readonly string Id = "Octopus.Environment.Id";
            public static readonly string Name = "Octopus.Environment.Name";
        }

        public static class Project
        {
            public static readonly string Id = "Octopus.Project.Id";
            public static readonly string Name = "Octopus.Project.Name";
        }

        public static class Features
        {
            public const string CustomScripts = "Octopus.Features.CustomScripts";
            public const string Vhd = "Octopus.Features.Vhd";
            public const string ConfigurationTransforms = "Octopus.Features.ConfigurationTransforms";
        }

        public static class Action
        {
            public const string Name = "Octopus.Action.Name";
            public const string SkipRemainingConventions = "Octopus.Action.SkipRemainingConventions";
            public const string SkipJournal = "Octopus.Action.SkipJournal";
            public const string AdditionalPaths = "Octopus.Action.AdditionalPaths";

            public static class Azure
            {
                public static readonly string UseBundledAzurePowerShellModules = "Octopus.Action.Azure.UseBundledAzurePowerShellModules";

                public static readonly string SubscriptionId = "Octopus.Action.Azure.SubscriptionId";
                public static readonly string ClientId = "Octopus.Action.Azure.ClientId";
                public static readonly string TenantId = "Octopus.Action.Azure.TenantId";
                public static readonly string Password = "Octopus.Action.Azure.Password";
                public static readonly string CertificateBytes = "Octopus.Action.Azure.CertificateBytes";
                public static readonly string CertificateThumbprint = "Octopus.Action.Azure.CertificateThumbprint";

                public static readonly string ServiceFabricConnectionEndpoint = "Octopus.Action.Azure.ServiceFabricConnectionEndpoint";
                public static readonly string ServiceFabricTargetProfile = "Octopus.Action.Azure.ServiceFabricTargetProfile";

                public static readonly string WebAppName = "Octopus.Action.Azure.WebAppName";
                public static readonly string RemoveAdditionalFiles = "Octopus.Action.Azure.RemoveAdditionalFiles";
                public static readonly string AppOffline = "Octopus.Action.Azure.AppOffline";
                public static readonly string PreserveAppData = "Octopus.Action.Azure.PreserveAppData";
                public static readonly string PreservePaths = "Octopus.Action.Azure.PreservePaths";
                public static readonly string PhysicalPath = "Octopus.Action.Azure.PhysicalPath";
                public static readonly string UseChecksum = "Octopus.Action.Azure.UseChecksum";

                public static readonly string CloudServiceName = "Octopus.Action.Azure.CloudServiceName";
                public static readonly string Slot = "Octopus.Action.Azure.Slot";
                public static readonly string SwapIfPossible = "Octopus.Action.Azure.SwapIfPossible";
                public static readonly string StorageAccountName = "Octopus.Action.Azure.StorageAccountName";
                public static readonly string UseCurrentInstanceCount = "Octopus.Action.Azure.UseCurrentInstanceCount";
                public static readonly string UploadedPackageUri = "Octopus.Action.Azure.UploadedPackageUri";
                public static readonly string CloudServicePackagePath = "Octopus.Action.Azure.CloudServicePackagePath";
                public static readonly string PackageExtractionPath = "Octopus.Action.Azure.PackageExtractionPath";
                public static readonly string CloudServicePackageExtractionDisabled = "Octopus.Action.Azure.CloudServicePackageExtractionDisabled";
                public static readonly string LogExtractedCspkg = "Octopus.Action.Azure.LogExtractedCspkg";
                public static readonly string CloudServiceConfigurationFileRelativePath = "Octopus.Action.Azure.CloudServiceConfigurationFileRelativePath";
                public static readonly string DeploymentLabel = "Octopus.Action.Azure.DeploymentLabel";

                public static readonly string ResourceGroupName = "Octopus.Action.Azure.ResourceGroupName";
                public static readonly string ResourceGroupDeploymentName = "Octopus.Action.Azure.ResourceGroupDeploymentName";
                public static readonly string ResourceGroupDeploymentMode = "Octopus.Action.Azure.ResourceGroupDeploymentMode";

                public static readonly string Environment = "Octopus.Action.Azure.Environment";
                public static readonly string ResourceManagementEndPoint = "Octopus.Action.Azure.ResourceManagementEndPoint";
                public static readonly string ServiceManagementEndPoint = "Octopus.Action.Azure.ServiceManagementEndPoint";
                public static readonly string ActiveDirectoryEndPoint = "Octopus.Action.Azure.ActiveDirectoryEndPoint";
                public static readonly string StorageEndPointSuffix = "Octopus.Action.Azure.StorageEndpointSuffix";

                public static class Output
                {
                    public static readonly string AzurePowershellModulePath = "OctopusAzureModulePath";
                    public static readonly string SubscriptionId = "OctopusAzureSubscriptionId";
                    public static readonly string ModulePath = "OctopusAzureModulePath";
                    public static readonly string ConfigurationFile = "OctopusAzureConfigurationFile";
                    public static readonly string CloudServiceDeploymentSwapped = "OctopusAzureCloudServiceDeploymentSwapped";
                }
            }

            public class WindowsService
            {
                public const string Arguments = "Octopus.Action.WindowsService.Arguments";
            }

            public static class PowerShell
            {
                public static readonly string CustomPowerShellVersion = "Octopus.Action.PowerShell.CustomPowerShellVersion";
                public static readonly string ExecuteWithoutProfile = "Octopus.Action.PowerShell.ExecuteWithoutProfile";
            }

            public static class Script
            {
                public const string SuppressEnvironmentLogging = "Octopus.Action.Script.SuppressEnvironmentLogging";
            }
        }

        public static class Machine
        {
            public const string Name = "Octopus.Machine.Name";
        }

        public static class Account
        {
            public const string Name = "Octopus.Account.Name";
            public const string AccountType = "Octopus.Account.AccountType";
        }

        public static class Release
        {
            public static readonly string Number = "Octopus.Release.Number";
        }
    }
}
