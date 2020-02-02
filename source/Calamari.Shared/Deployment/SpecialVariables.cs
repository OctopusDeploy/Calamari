using System;
using System.ComponentModel;
using System.Linq;
using Calamari.Integration.Scripting;
using Octostache;

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

        public static ScriptSyntax GetLibraryScriptModuleLanguage(VariableDictionary variables, string variableName)
        {
            var expectedName = variableName.Replace("Octopus.Script.Module[", "Octopus.Script.Module.Language[");
            var syntaxVariable = variables.GetNames().FirstOrDefault(x => x == expectedName);
            if (syntaxVariable == null)
                return ScriptSyntax.PowerShell;
            return (ScriptSyntax) Enum.Parse(typeof(ScriptSyntax), variables[syntaxVariable]);
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
        
        public static readonly string CopyWorkingDirectoryIncludingKeyTo = "Octopus.Calamari.CopyWorkingDirectoryIncludingKeyTo";
        public static readonly string CalamariWorkingDirectory = "OctopusCalamariWorkingDirectory";

        public const string AdditionalVariablesPath = Environment.Prefix + "Octopus.AdditionalVariablesPath";
        
        public static class Bootstrapper
        {
            public static string ModulePaths = "Octopus.Calamari.Bootstrapper.ModulePaths";
        }

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

            public static class PreviousSuccessfulInstallation
            {
                public static readonly string PackageVersion = "Octopus.Tentacle.PreviousSuccessfulInstallation.PackageVersion";
                public static readonly string PackageFilePath = "Octopus.Tentacle.PreviousSuccessfulInstallation.PackageFilePath";
                public static readonly string OriginalInstalledPath = "Octopus.Tentacle.PreviousSuccessfulInstallation.OriginalInstalledPath";
                public static readonly string CustomInstallationDirectory = "Octopus.Tentacle.PreviousSuccessfulInstallation.CustomInstallationDirectory";
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
            public static string Id = "Octopus.Deployment.Id";
            
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
            public static readonly string CustomPackageFileName = "Octopus.Action.Package.CustomPackageFileName";
            public static readonly string CustomInstallationDirectoryShouldBePurgedBeforeDeployment = "Octopus.Action.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment";
            public static readonly string CustomInstallationDirectoryPurgeExclusions = "Octopus.Action.Package.CustomInstallationDirectoryPurgeExclusions";
            public static readonly string AutomaticallyUpdateAppSettingsAndConnectionStrings = "Octopus.Action.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings";
            public static readonly string JsonConfigurationVariablesEnabled = "Octopus.Action.Package.JsonConfigurationVariablesEnabled";
            public static readonly string JsonConfigurationVariablesTargets = "Octopus.Action.Package.JsonConfigurationVariablesTargets";
            public static readonly string AutomaticallyRunConfigurationTransformationFiles = "Octopus.Action.Package.AutomaticallyRunConfigurationTransformationFiles";
            public static readonly string TreatConfigTransformationWarningsAsErrors = "Octopus.Action.Package.TreatConfigTransformationWarningsAsErrors";
            public static readonly string IgnoreConfigTransformationErrors = "Octopus.Action.Package.IgnoreConfigTransformationErrors";
            public static readonly string SuppressConfigTransformationLogging = "Octopus.Action.Package.SuppressConfigTransformationLogging";
            public static readonly string EnableDiagnosticsConfigTransformationLogging = "Octopus.Action.Package.EnableDiagnosticsConfigTransformationLogging";
            public static readonly string AdditionalXmlConfigurationTransforms = "Octopus.Action.Package.AdditionalXmlConfigurationTransforms";
            public static readonly string SubstituteInFilesEnabled = "Octopus.Action.SubstituteInFiles.Enabled";
            public static readonly string SubstituteInFilesTargets = "Octopus.Action.SubstituteInFiles.TargetFiles";
            public static readonly string EnableNoMatchWarning = "Octopus.Action.SubstituteInFiles.EnableNoMatchWarning";
            public static readonly string SubstituteInFilesOutputEncoding = "Octopus.Action.SubstituteInFiles.OutputEncoding";
            public static readonly string SkipIfAlreadyInstalled = "Octopus.Action.Package.SkipIfAlreadyInstalled";
            public static readonly string IgnoreVariableReplacementErrors = "Octopus.Action.Package.IgnoreVariableReplacementErrors";
            public static readonly string SuppressNestedScriptWarning = "OctopusSuppressNestedScriptWarning";
            public static readonly string RunPackageScripts = "Octopus.Action.Package.RunScripts";

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

        public static class Packages
        {
            public static readonly string PackageCollection = "Octopus.Action.Package";

            public static string PackageId(string key)
            {
                return $"Octopus.Action.Package[{key}].PackageId";
            }

            public static string PackageVersion(string key)
            {
                return $"Octopus.Action.Package[{key}].PackageVersion";
            }

            public static string OriginalPath(string key)
            {
                return $"Octopus.Action.Package[{key}].OriginalPath";
            }
            
            public static string Extract(string key)
            {
                return $"Octopus.Action.Package[{key}].Extract";
            }

            public static string ExtractedPath(string key)
            {
                return $"Octopus.Action.Package[{key}].ExtractedPath";
            }
            
            public static string PackageFileName(string key)
            {
                return $"Octopus.Action.Package[{key}].PackageFileName";
            }

            public static string PackageFilePath(string key)
            {
                return $"Octopus.Action.Package[{key}].PackageFilePath";
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
            public const string Prefix = "env:";
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
            public const string FailScriptOnErrorOutput = "Octopus.Action.FailScriptOnErrorOutput";            

            public static class IisWebSite
            {
                public static readonly string DeployAsWebSite = "Octopus.Action.IISWebSite.CreateOrUpdateWebSite"; 
                public static readonly string DeployAsWebApplication = "Octopus.Action.IISWebSite.WebApplication.CreateOrUpdate"; 
                public static readonly string DeployAsVirtualDirectory = "Octopus.Action.IISWebSite.VirtualDirectory.CreateOrUpdate";

                public static readonly string ApplicationPoolName = "Octopus.Action.IISWebSite.ApplicationPoolName";
                public static readonly string ApplicationPoolUserName = "Octopus.Action.IISWebSite.ApplicationPoolUsername";
                public static readonly string Bindings = "Octopus.Action.IISWebSite.Bindings";
                public static readonly string ExistingBindings = "Octopus.Action.IISWebSite.ExistingBindings";
                public static readonly string ApplicationPoolIdentityType = "Octopus.Action.IISWebSite.ApplicationPoolIdentityType";

                public static class Output
                {
                    public static readonly string CertificateStoreName = "Octopus.Action.IISWebSite.CertificateStoreName";
                }
            }

            public static class ServiceFabric
            {
                public static readonly string ConnectionEndpoint = "Octopus.Action.ServiceFabric.ConnectionEndpoint";
                public static readonly string SecurityMode = "Octopus.Action.ServiceFabric.SecurityMode";
                public static readonly string ServerCertThumbprint = "Octopus.Action.ServiceFabric.ServerCertThumbprint";
                public static readonly string ClientCertVariable = "Octopus.Action.ServiceFabric.ClientCertVariable";
                public static readonly string CertificateFindType = "Octopus.Action.ServiceFabric.CertificateFindType";
                public static readonly string CertificateFindValueOverride = "Octopus.Action.ServiceFabric.CertificateFindValueOverride";
                public static readonly string CertificateStoreLocation = "Octopus.Action.ServiceFabric.CertificateStoreLocation";
                public static readonly string CertificateStoreName = "Octopus.Action.ServiceFabric.CertificateStoreName";
                public static readonly string PublishProfileFile = "Octopus.Action.ServiceFabric.PublishProfileFile";
                public static readonly string ApplicationPackagePath = "Octopus.Action.ServiceFabric.ApplicationPackagePath";
                public static readonly string DeployOnly = "Octopus.Action.ServiceFabric.DeployOnly";
                public static readonly string UnregisterUnusedApplicationVersionsAfterUpgrade = "Octopus.Action.ServiceFabric.UnregisterUnusedApplicationVersionsAfterUpgrade";
                public static readonly string OverrideUpgradeBehavior = "Octopus.Action.ServiceFabric.OverrideUpgradeBehavior";
                public static readonly string OverwriteBehavior = "Octopus.Action.ServiceFabric.OverwriteBehavior";
                public static readonly string SkipPackageValidation = "Octopus.Action.ServiceFabric.SkipPackageValidation";
                public static readonly string CopyPackageTimeoutSec = "Octopus.Action.ServiceFabric.CopyPackageTimeoutSec";
                public static readonly string RegisterApplicationTypeTimeoutSec = "Octopus.Action.ServiceFabric.RegisterApplicationTypeTimeoutSec";
                public static readonly string LogExtractedApplicationPackage = "Octopus.Action.ServiceFabric.LogExtractedApplicationPackage";
                public static readonly string AadCredentialType = "Octopus.Action.ServiceFabric.AadCredentialType";
                public static readonly string AadUserCredentialUsername = "Octopus.Action.ServiceFabric.AadUserCredentialUsername";
                public static readonly string AadUserCredentialPassword = "Octopus.Action.ServiceFabric.AadUserCredentialPassword";
                public static readonly string AadClientCredentialSecret = "Octopus.Action.ServiceFabric.AadClientCredentialSecret";
            }

            public static class Aws
            {
                public static readonly string CloudFormationStackName = "Octopus.Action.Aws.CloudFormationStackName";
                public static readonly string CloudFormationTemplate =  "Octopus.Action.Aws.CloudFormationTemplate";
                public static readonly string CloudFormationProperties = "Octopus.Action.Aws.CloudFormationProperties";
                public static readonly string AssumeRoleARN = "Octopus.Action.Aws.AssumedRoleArn";
                public static readonly string AccountId = "Octopus.Action.AwsAccount.Variable";
                public static readonly string CloudFormationAction = "Octopus.Action.Aws.CloudFormationAction";
            }

            public static class Azure
            {
                public static readonly string UseBundledAzurePowerShellModules = "Octopus.Action.Azure.UseBundledAzurePowerShellModules";

                public static readonly string AccountVariable = "Octopus.Action.AzureAccount.Variable";

                public static readonly string SubscriptionId = "Octopus.Action.Azure.SubscriptionId";
                public static readonly string ClientId = "Octopus.Action.Azure.ClientId";
                public static readonly string TenantId = "Octopus.Action.Azure.TenantId";
                public static readonly string Password = "Octopus.Action.Azure.Password";
                public static readonly string CertificateBytes = "Octopus.Action.Azure.CertificateBytes";
                public static readonly string CertificateThumbprint = "Octopus.Action.Azure.CertificateThumbprint";

                public static readonly string WebAppName = "Octopus.Action.Azure.WebAppName";
                public static readonly string WebAppSlot = "Octopus.Action.Azure.DeploymentSlot";
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
                    public static readonly string SubscriptionId = "OctopusAzureSubscriptionId";
                    public static readonly string ConfigurationFile = "OctopusAzureConfigurationFile";
                    public static readonly string CloudServiceDeploymentSwapped = "OctopusAzureCloudServiceDeploymentSwapped";
                }
            }

            public class WindowsService
            {
                public const string Arguments = "Octopus.Action.WindowsService.Arguments";
                public const string CustomAccountName = "Octopus.Action.WindowsService.CustomAccountName";
                public const string CustomAccountPassword = "Octopus.Action.WindowsService.CustomAccountPassword";
            }

            public static class PowerShell
            {
                public static readonly string CustomPowerShellVersion = "Octopus.Action.PowerShell.CustomPowerShellVersion";
                public static readonly string ExecuteWithoutProfile = "Octopus.Action.PowerShell.ExecuteWithoutProfile";
                public static readonly string UserName = "Octopus.Action.PowerShell.UserName";
                public static readonly string Password = "Octopus.Action.PowerShell.Password";
                public static readonly string DebugMode = "Octopus.Action.PowerShell.DebugMode";
                public static readonly string Edition = "Octopus.Action.PowerShell.Edition";

                public static class PSDebug
                {
                    public static readonly string Trace = "Octopus.Action.PowerShell.PSDebug.Trace";
                    public static readonly string Strict = "Octopus.Action.PowerShell.PSDebug.Strict";
                }
            }

            public static class Certificate
            {
                public static readonly string CertificateVariable = "Octopus.Action.Certificate.Variable";
                public static readonly string PrivateKeyExportable = "Octopus.Action.Certificate.PrivateKeyExportable";
                public static readonly string StoreLocation = "Octopus.Action.Certificate.StoreLocation";
                public static readonly string StoreName = "Octopus.Action.Certificate.StoreName";
                public static readonly string StoreUser = "Octopus.Action.Certificate.StoreUser";
            }

            public static class Script
            {
                public static readonly string Syntax = "Octopus.Action.Script.Syntax";
                public static readonly string ScriptBody = "Octopus.Action.Script.ScriptBody";
                public static readonly string ScriptFileName = "Octopus.Action.Script.ScriptFileName";
                public static readonly string ScriptParameters = "Octopus.Action.Script.ScriptParameters";
                public static readonly string ScriptSource = "Octopus.Action.Script.ScriptSource";
                public static readonly string ExitCode = "Octopus.Action.Script.ExitCode";
                public static readonly string Timeout = "Octopus.Action.Script.Timeout";

                public static string ScriptBodyBySyntax(ScriptSyntax syntax)
                {
                    return $"Octopus.Action.Script.ScriptBody[{syntax.ToString()}]";
                }
            }
            
            public static class CustomScripts
            {
                public static readonly string Prefix = "Octopus.Action.CustomScripts.";

                public static string GetCustomScriptStage(string deploymentStage, ScriptSyntax scriptSyntax)
                {
                    return $"{Prefix}{deploymentStage}.{scriptSyntax.FileExtension()}";
                }
            }

            public static class Java
            {
                public static readonly string JavaLibraryEnvVar = "JavaIntegrationLibraryPackagePath";

                public static readonly string JavaArchiveExtractionDisabled =
                    "Octopus.Action.Java.JavaArchiveExtractionDisabled";

                public static readonly string DeployExploded = "Octopus.Action.JavaArchive.DeployExploded";

                public static class Tomcat
                {
                    public static readonly string Feature = "Octopus.Features.TomcatDeployManager";
                    public static readonly string DeployName = "Tomcat.Deploy.Name";
                    public static readonly string Controller = "Tomcat.Deploy.Controller";
                    public static readonly string User = "Tomcat.Deploy.User";
                    public static readonly string Password = "Tomcat.Deploy.Password";
                    public static readonly string Enabled = "Tomcat.Deploy.Enabled";
                    public static readonly string Version = "Tomcat.Deploy.Version";
                    public static readonly string StateActionTypeName = "Octopus.TomcatState";
                }

                public static class TomcatDeployCertificate
                {
                    public static readonly string CertificateActionTypeName = "Octopus.TomcatDeployCertificate";
                    public static readonly string CatalinaHome = "Tomcat.Certificate.CatalinaHome";
                    public static readonly string CatalinaBase = "Tomcat.Certificate.CatalinaBase";
                    public static readonly string Implementation = "Tomcat.Certificate.Implementation";
                    public static readonly string PrivateKeyFilename = "Tomcat.Certificate.PrivateKeyFilename";
                    public static readonly string PublicKeyFilename = "Tomcat.Certificate.PublicKeyFilename";
                    public static readonly string Service = "Tomcat.Certificate.Service";
                    public static readonly string Port = "Tomcat.Certificate.Port";
                    public static readonly string Hostname = "Tomcat.Certificate.Hostname";
                    public static readonly string Default = "Tomcat.Certificate.Default";
                }

                public static class JavaKeystore
                {
                    public static readonly string CertificateActionTypeName = "Octopus.JavaDeployCertificate";
                    public static readonly string Variable = "Java.Certificate.Variable";
                    public static readonly string Password = "Java.Certificate.Password";
                    public static readonly string KeystoreFilename = "Java.Certificate.KeystoreFilename";
                    public static readonly string KeystoreAlias = "Java.Certificate.KeystoreAlias";
                }


                public static class WildFly
                {
                    public static readonly string Feature = "Octopus.Features.WildflyDeployCLI";
                    public static readonly string StateFeature = "Octopus.Features.WildflyStateCLI";
                    public static readonly string DeployName = "WildFly.Deploy.Name";
                    public static readonly string Controller = "WildFly.Deploy.Controller";
                    public static readonly string Port = "WildFly.Deploy.Port";
                    public static readonly string User = "WildFly.Deploy.User";
                    public static readonly string Password = "WildFly.Deploy.Password";
                    public static readonly string Protocol = "WildFly.Deploy.Protocol";
                    public static readonly string Enabled = "WildFly.Deploy.Enabled";
                    public static readonly string EnabledServerGroup = "WildFly.Deploy.EnabledServerGroup";
                    public static readonly string DisabledServerGroup = "WildFly.Deploy.DisabledServerGroup";
                    public static readonly string ServerType = "WildFly.Deploy.ServerType";
                    public static readonly string DeployActionTypeName = "Octopus.WildFlyDeploy";
                    public static readonly string CertificateActionTypeName = "Octopus.WildFlyCertificateDeploy";
                    public static readonly string StateActionTypeName = "Octopus.WildFlyState";
                    public static readonly string CertificateProfiles = "WildFly.Deploy.CertificateProfiles";
                    public static readonly string DeployCertificate = "WildFly.Deploy.DeployCertificate";
                    public static readonly string CertificateRelativeTo = "WildFly.Deploy.CertificateRelativeTo";
                    public static readonly string HTTPSPortBindingName = "WildFly.Deploy.HTTPSPortBindingName";
                    public static readonly string SecurityRealmName = "WildFly.Deploy.SecurityRealmName";
                    public static readonly string ElytronKeystoreName = "WildFly.Deploy.ElytronKeystoreName";
                    public static readonly string ElytronKeymanagerName = "WildFly.Deploy.ElytronKeymanagerName";
                    public static readonly string ElytronSSLContextName = "WildFly.Deploy.ElytronSSLContextName";
                }
            }

            public static class Nginx
            {
                public static readonly string ConfigRoot = "Octopus.Action.Nginx.ConfigurationsDirectory";
                public static readonly string SslRoot = "Octopus.Action.Nginx.CertificatesDirectory";
                
                public static class Server
                {
                    public static readonly string HostName = "Octopus.Action.Nginx.Server.HostName";
                    public static readonly string Bindings = "Octopus.Action.Nginx.Server.Bindings";
                    public static readonly string Locations = "Octopus.Action.Nginx.Server.Locations";
                }
            }
        }

        public static class Machine
        {
            public const string Name = "Octopus.Machine.Name";
            public const string DeploymentTargetType = "Octopus.Machine.DeploymentTargetType";
        }

        public static class Account
        {
            public const string Name = "Octopus.Account.Name";
            public const string AccountType = "Octopus.Account.AccountType";
            public const string Username = "Octopus.Account.Username";
            public const string Password = "Octopus.Account.Password";
            public const string Token = "Octopus.Account.Token";
        }

        public static class Release
        {
            public static readonly string Number = "Octopus.Release.Number";
        }

        public static class Certificate
        {
            public static class Properties
            {
                public static readonly string Thumbprint = "Thumbprint";
                public static readonly string Pfx = "Pfx";
                public static readonly string Password = "Password";
                public static readonly string Subject = "Subject";
            }
            
            public static readonly string PrivateKeyAccessRules =
                "Octopus.Action.Certificate.PrivateKeyAccessRules";


            public static string Name(string variableName)
            {
                return $"{variableName}.Name";
            }

            public static string CertificatePem(string variableName)
            {
                return $"{variableName}.CertificatePem";
            }

            public static string PrivateKey(string variableName)
            {
                return $"{variableName}.PrivateKey";
            }

            public static string PrivateKeyPem(string variableName)
            {
                return $"{variableName}.PrivateKeyPem";
            }

            public static string Subject(string variableName)
            {
                return $"{variableName}.Subject";
            }
        }
    }
}
