using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Variables;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;

namespace Calamari.CommonTemp
{
    internal class ConfigurationTransformsBehaviour : IBehaviour
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IConfigurationTransformer configurationTransformer;
        readonly ITransformFileLocator transformFileLocator;
        readonly ILog log;

        public ConfigurationTransformsBehaviour(ICalamariFileSystem fileSystem, IConfigurationTransformer configurationTransformer, ITransformFileLocator transformFileLocator, ILog log)
        {
            this.fileSystem = fileSystem;
            this.configurationTransformer = configurationTransformer;
            this.transformFileLocator = transformFileLocator;
            this.log = log;
        }

        public Task Execute(RunningDeployment deployment)
        {
            var features = deployment.Variables.GetStrings(KnownVariables.Package.EnabledFeatures).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            if (!features.Contains(KnownVariables.Features.ConfigurationTransforms))
                return this.CompletedTask();

            var explicitTransforms = GetExplicitTransforms(deployment);
            var automaticTransforms = GetAutomaticTransforms(deployment);
            var sourceExtensions = GetSourceExtensions(deployment, explicitTransforms);

            var allTransforms = explicitTransforms.Concat(automaticTransforms).ToList();
            var transformDefinitionsApplied = new List<XmlConfigTransformDefinition>();
            var duplicateTransformDefinitions = new List<XmlConfigTransformDefinition>();
            var transformFilesApplied = new HashSet<Tuple<string, string>>();
            var diagnosticLoggingEnabled = deployment.Variables.GetFlag(KnownVariables.Package.EnableDiagnosticsConfigTransformationLogging);

            if (diagnosticLoggingEnabled)
                log.Verbose($"Recursively searching for transformation files that match {string.Join(" or ", sourceExtensions)} in folder '{deployment.CurrentDirectory}'");
            foreach (var configFile in MatchingFiles(deployment, sourceExtensions))
            {
                if (diagnosticLoggingEnabled)
                    log.Verbose($"Found config file '{configFile}'");
                ApplyTransformations(configFile, allTransforms, transformFilesApplied,
                    transformDefinitionsApplied, duplicateTransformDefinitions, diagnosticLoggingEnabled, deployment);
            }

            LogFailedTransforms(explicitTransforms, automaticTransforms, transformDefinitionsApplied, duplicateTransformDefinitions, diagnosticLoggingEnabled);
            deployment.Variables.SetStrings(KnownVariables.AppliedXmlConfigTransforms, transformFilesApplied.Select(t => t.Item1), "|");

            return this.CompletedTask();
        }

        static List<XmlConfigTransformDefinition> GetAutomaticTransforms(RunningDeployment deployment)
        {
            var result = new List<XmlConfigTransformDefinition>();
            if (deployment.Variables.GetFlag(KnownVariables.Package.AutomaticallyRunConfigurationTransformationFiles))
            {
                result.Add(new XmlConfigTransformDefinition("Release"));

                var environment = deployment.Variables.Get(
                    DeploymentEnvironment.Name);
                if (!string.IsNullOrWhiteSpace(environment))
                {
                    result.Add(new XmlConfigTransformDefinition(environment));
                }

                var tenant = deployment.Variables.Get(DeploymentVariables.Tenant.Name);
                if (!string.IsNullOrWhiteSpace(tenant))
                {
                    result.Add(new XmlConfigTransformDefinition(tenant));
                }
            }
            return result;
        }

        static List<XmlConfigTransformDefinition> GetExplicitTransforms(RunningDeployment deployment)
        {
            var transforms = deployment.Variables.Get(KnownVariables.Package.AdditionalXmlConfigurationTransforms);

            if (string.IsNullOrWhiteSpace(transforms))
                return new List<XmlConfigTransformDefinition>();

            return transforms
                .Split(',', '\r', '\n')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => new XmlConfigTransformDefinition(s))
                .ToList();
        }

        void ApplyTransformations(string sourceFile,
            IEnumerable<XmlConfigTransformDefinition> transformations,
            ISet<Tuple<string, string>> transformFilesApplied,
            IList<XmlConfigTransformDefinition> transformDefinitionsApplied,
            IList<XmlConfigTransformDefinition> duplicateTransformDefinitions,
            bool diagnosticLoggingEnabled,
            RunningDeployment deployment)
        {
            foreach (var transformation in transformations)
            {
                if (diagnosticLoggingEnabled)
                    log.Verbose($" - checking against transform '{transformation}'");
                if (transformation.IsTransformWildcard && !sourceFile.EndsWith(GetFileName(transformation.SourcePattern!), StringComparison.OrdinalIgnoreCase))
                {
                    if (diagnosticLoggingEnabled)
                        log.Verbose($" - skipping transform as its a wildcard transform and source file \'{sourceFile}\' does not end with \'{GetFileName(transformation.SourcePattern!)}\'");
                    continue;
                }

                try
                {
                    ApplyTransformations(sourceFile, transformation, transformFilesApplied,
                        transformDefinitionsApplied, duplicateTransformDefinitions, diagnosticLoggingEnabled, deployment);
                }
                catch (Exception)
                {
                    log.ErrorFormat("Could not transform the file '{0}' using the {1}pattern '{2}'.", sourceFile, transformation.IsTransformWildcard ? "wildcard " : "", transformation.TransformPattern);
                    throw;
                }
            }
        }

        void ApplyTransformations(string sourceFile,
            XmlConfigTransformDefinition transformation,
            ISet<Tuple<string, string>> transformFilesApplied,
            ICollection<XmlConfigTransformDefinition> transformDefinitionsApplied,
            ICollection<XmlConfigTransformDefinition> duplicateTransformDefinitions,
            bool diagnosticLoggingEnabled,
            RunningDeployment deployment)
        {
            if (transformation == null)
                return;

            var transformFileNames = transformFileLocator.DetermineTransformFileNames(sourceFile, transformation, diagnosticLoggingEnabled, deployment)
                .Distinct()
                .ToArray();

            foreach (var transformFile in transformFileNames)
            {
                var transformFiles = new Tuple<string, string>(transformFile, sourceFile);
                if (transformFilesApplied.Contains(transformFiles))
                {
                    if (diagnosticLoggingEnabled)
                        log.Verbose($" - Skipping as target \'{sourceFile}\' has already been transformed by transform \'{transformFile}\'");

                    duplicateTransformDefinitions.Add(transformation);
                    continue;
                }

                log.Info($"Transforming '{sourceFile}' using '{transformFile}'.");
                configurationTransformer.PerformTransform(sourceFile, transformFile, sourceFile);

                transformFilesApplied.Add(transformFiles);
                transformDefinitionsApplied.Add(transformation);
            }
        }

        static string[] GetSourceExtensions(RunningDeployment deployment, List<XmlConfigTransformDefinition> transformDefinitions)
        {
            var extensions = new HashSet<string>();

            if (deployment.Variables.GetFlag(KnownVariables.Package.AutomaticallyRunConfigurationTransformationFiles))
            {
                extensions.Add("*.config");
            }

            foreach (var definition in transformDefinitions
                .Where(transform => transform.Advanced)
                .Select(transform => "*" + Path.GetExtension(transform.SourcePattern))
                .Distinct())
            {
                extensions.Add(definition);
            }

            return extensions.ToArray();
        }

        void LogFailedTransforms(IEnumerable<XmlConfigTransformDefinition> configTransform,
            List<XmlConfigTransformDefinition> automaticTransforms,
            List<XmlConfigTransformDefinition> transformDefinitionsApplied,
            List<XmlConfigTransformDefinition> duplicateTransformDefinitions,
            bool diagnosticLoggingEnabled)
        {
            foreach (var transform in configTransform.Except(transformDefinitionsApplied).Except(duplicateTransformDefinitions).Select(trans => trans.ToString()).Distinct())
            {
                log.Verbose($"The transform pattern \"{transform}\" was not performed as no matching files could be found.");
                if (!diagnosticLoggingEnabled)
                    log.Verbose("For detailed diagnostic logging, please set a variable 'Octopus.Action.Package.EnableDiagnosticsConfigTransformationLogging' with a value of 'True'.");
            }

            foreach (var transform in duplicateTransformDefinitions.Except(automaticTransforms).Select(trans => trans.ToString()).Distinct())
            {
                log.VerboseFormat("The transform pattern \"{0}\" was not performed as it overlapped with another transform.", transform);
            }
        }

        static string GetFileName(string path)
        {
            return Path.GetFileName(path) ?? string.Empty;
        }

        List<string> MatchingFiles(RunningDeployment deployment, string[] sourceExtensions)
        {
            var files = fileSystem.EnumerateFilesRecursively(deployment.CurrentDirectory, sourceExtensions).ToList();

            foreach (var path in deployment.Variables.GetStrings(ActionVariables.AdditionalPaths).Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var pathFiles = fileSystem.EnumerateFilesRecursively(path, sourceExtensions);
                files.AddRange(pathFiles);
            }

            return files;
        }
    }
}