using System;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;

namespace Calamari.Common.Plumbing.Variables
{
    public class KnownVariables
    {
        public static readonly string RetentionPolicySet = "OctopusRetentionPolicySet";
        public static readonly string PrintVariables = "OctopusPrintVariables";
        public static readonly string PrintEvaluatedVariables = "OctopusPrintEvaluatedVariables";
        public static readonly string OriginalPackageDirectoryPath = "OctopusOriginalPackageDirectoryPath";
        public static readonly string DeleteScriptsOnCleanup = "OctopusDeleteScriptsOnCleanup";
        public static readonly string AppliedXmlConfigTransforms = "OctopusAppliedXmlConfigTransforms";
        public static readonly string ShouldFailDeploymentOnSubstitutionFails = "ShouldFailDeploymentOnSubstitutionFails";

        public static readonly string NugetHttpTimeout = "OctopusNugetHttpTimeout";

        public const string EnabledFeatureToggles = "OctopusEnabledFeatureToggles";

        public static class Action
        {
            public const string SkipJournal = "Octopus.Action.SkipJournal";
            public const string SkipRemainingConventions = "Octopus.Action.SkipRemainingConventions";
            public const string FailScriptOnErrorOutput = "Octopus.Action.FailScriptOnErrorOutput";

            public static class CustomScripts
            {
                public static readonly string Prefix = "Octopus.Action.CustomScripts.";

                public static string GetCustomScriptStage(string deploymentStage, ScriptSyntax scriptSyntax)
                {
                    return $"{Prefix}{deploymentStage}.{scriptSyntax.FileExtension()}";
                }
            }
            

            public static class GitResource
            {
                public const string CommitHash = "Octopus.Action.GitResource.CommitHash";
            }
        }

        public static class Calamari
        {
            public const string WaitForDebugger = "Octopus.Calamari.WaitForDebugger";
            public const string EnablePackageRetention = "Octopus.Calamari.EnablePackageRetention";
            public const string PackageRetentionJournalPath = "env:CalamariPackageRetentionJournalPath";
            public const string PackageRetentionLockExpiration = "Octopus.Calamari.PackageRetentionLockExpiration";
        }

        public static class Release
        {
            public static readonly string Number = "Octopus.Release.Number";
        }

        public static class ServerTask
        {
            public static readonly string Id = "Octopus.Task.Id";
        }

        public static class Features
        {
            public const string CustomScripts = "Octopus.Features.CustomScripts";
            public const string ConfigurationVariables = "Octopus.Features.ConfigurationVariables";
            public const string ConfigurationTransforms = "Octopus.Features.ConfigurationTransforms";
            public const string SubstituteInFiles = "Octopus.Features.SubstituteInFiles";
            public const string StructuredConfigurationVariables = "Octopus.Features.JsonConfigurationVariables";
        }

        public static class Package
        {
            public static readonly string ShouldDownloadOnTentacle = "Octopus.Action.Package.DownloadOnTentacle";
            public static readonly string EnabledFeatures = "Octopus.Action.EnabledFeatures";
            public static readonly string UpdateIisWebsite = "Octopus.Action.Package.UpdateIisWebsite";
            public static readonly string UpdateIisWebsiteName = "Octopus.Action.Package.UpdateIisWebsiteName";
            public static readonly string AutomaticallyUpdateAppSettingsAndConnectionStrings = "Octopus.Action.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings";
            public static readonly string JsonConfigurationVariablesEnabled = "Octopus.Action.Package.JsonConfigurationVariablesEnabled";
            public static readonly string JsonConfigurationVariablesTargets = "Octopus.Action.Package.JsonConfigurationVariablesTargets";
            public static readonly string AutomaticallyRunConfigurationTransformationFiles = "Octopus.Action.Package.AutomaticallyRunConfigurationTransformationFiles";
            public static readonly string TreatConfigTransformationWarningsAsErrors = "Octopus.Action.Package.TreatConfigTransformationWarningsAsErrors";
            public static readonly string IgnoreConfigTransformationErrors = "Octopus.Action.Package.IgnoreConfigTransformationErrors";
            public static readonly string SuppressConfigTransformationLogging = "Octopus.Action.Package.SuppressConfigTransformationLogging";
            public static readonly string EnableDiagnosticsConfigTransformationLogging = "Octopus.Action.Package.EnableDiagnosticsConfigTransformationLogging";
            public static readonly string AdditionalXmlConfigurationTransforms = "Octopus.Action.Package.AdditionalXmlConfigurationTransforms";
            public static readonly string SkipIfAlreadyInstalled = "Octopus.Action.Package.SkipIfAlreadyInstalled";
            public static readonly string IgnoreVariableReplacementErrors = "Octopus.Action.Package.IgnoreVariableReplacementErrors";
            public static readonly string RunPackageScripts = "Octopus.Action.Package.RunScripts";

            public static class ArchiveLimits
            {
                public const string Enabled = "Octopus.Calamari.ArchiveLimits.Enabled";
                public const string MaximumUncompressedSize = "Octopus.Calamari.ArchiveLimits.MaximumUncompressedSize";
                public const string MaximumCompressionRatio = "Octopus.Calamari.ArchiveLimits.MaximumCompressionRatio";
                public const string OverrunPreventionEnabled = "Octopus.Calamari.ArchiveLimits.OverrunPrevention.Enabled";
                public const string MetricsEnabled = "Octopus.Calamari.ArchiveLimits.Metrics.Enabled";
            }
        }
    }
}