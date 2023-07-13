using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Common.Features.Behaviours
{
    public class ConfigurationTransformsBehaviour : IBehaviour
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IVariables variables;
        readonly IConfigurationTransformer configurationTransformer;
        readonly ITransformFileLocator transformFileLocator;
        readonly ILog log;
        private readonly string subdirectory;

        public ConfigurationTransformsBehaviour(
            ICalamariFileSystem fileSystem,
            IVariables variables,
            IConfigurationTransformer configurationTransformer,
            ITransformFileLocator transformFileLocator,
            ILog log,
            string subdirectory = "")
        {
            this.fileSystem = fileSystem;
            this.variables = variables;
            this.configurationTransformer = configurationTransformer;
            this.transformFileLocator = transformFileLocator;
            this.log = log;
            this.subdirectory = subdirectory;
        }

        public bool IsEnabled(RunningDeployment deployment)
        {
            return deployment.Variables.IsFeatureEnabled(KnownVariables.Features.ConfigurationTransforms);
        }

        public Task Execute(RunningDeployment deployment)
        {
            DoTransforms(Path.Combine(deployment.CurrentDirectory, subdirectory));

            return this.CompletedTask();
        }

        public void DoTransforms(string currentDirectory)
        {
            var explicitTransforms = GetExplicitTransforms();
            var automaticTransforms = GetAutomaticTransforms();
            var sourceExtensions = GetSourceExtensions(explicitTransforms);

            var allTransforms = explicitTransforms.Concat(automaticTransforms).ToList();
            var transformDefinitionsApplied = new List<XmlConfigTransformDefinition>();
            var duplicateTransformDefinitions = new List<XmlConfigTransformDefinition>();
            var transformFilesApplied = new HashSet<Tuple<string, string>>();
            var diagnosticLoggingEnabled = variables.GetFlag(KnownVariables.Package.EnableDiagnosticsConfigTransformationLogging);

            if (diagnosticLoggingEnabled)
                log.Verbose($"Recursively searching for transformation files that match {string.Join(" or ", sourceExtensions)} in folder '{currentDirectory}'");
            foreach (var configFile in MatchingFiles(currentDirectory, sourceExtensions))
            {
                if (diagnosticLoggingEnabled)
                    log.Verbose($"Found config file '{configFile}'");
                ApplyTransformations(configFile, allTransforms, transformFilesApplied,
                                     transformDefinitionsApplied, duplicateTransformDefinitions, diagnosticLoggingEnabled, currentDirectory);
            }

            LogFailedTransforms(explicitTransforms, automaticTransforms, transformDefinitionsApplied, duplicateTransformDefinitions, diagnosticLoggingEnabled);
            variables.SetStrings(KnownVariables.AppliedXmlConfigTransforms, transformFilesApplied.Select(t => t.Item1), "|");
        }

        List<XmlConfigTransformDefinition> GetAutomaticTransforms()
        {
            var result = new List<XmlConfigTransformDefinition>();
            if (variables.GetFlag(KnownVariables.Package.AutomaticallyRunConfigurationTransformationFiles))
            {
                result.Add(new XmlConfigTransformDefinition("Release"));

                var environment = variables.Get(
                    DeploymentEnvironment.Name);
                if (!string.IsNullOrWhiteSpace(environment))
                {
                    result.Add(new XmlConfigTransformDefinition(environment));
                }

                var tenant = variables.Get(DeploymentVariables.Tenant.Name);
                if (!string.IsNullOrWhiteSpace(tenant))
                {
                    result.Add(new XmlConfigTransformDefinition(tenant));
                }
            }
            return result;
        }

        List<XmlConfigTransformDefinition> GetExplicitTransforms()
        {
            var transforms = variables.Get(KnownVariables.Package.AdditionalXmlConfigurationTransforms);

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
            string currentDirectory)
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
                        transformDefinitionsApplied, duplicateTransformDefinitions, diagnosticLoggingEnabled, currentDirectory);
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
            string currentDirectory)
        {
            if (transformation == null)
                return;

            var transformFileNames = transformFileLocator.DetermineTransformFileNames(sourceFile, transformation, diagnosticLoggingEnabled, currentDirectory)
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

        string[] GetSourceExtensions(List<XmlConfigTransformDefinition> transformDefinitions)
        {
            var extensions = new HashSet<string>();

            if (variables.GetFlag(KnownVariables.Package.AutomaticallyRunConfigurationTransformationFiles))
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

        [return: NotNullIfNotNull("path")]
        static string? GetFileName(string? path)
        {
            return Path.GetFileName(path);
        }

        List<string> MatchingFiles(string currentDirectory, string[] sourceExtensions)
        {
            var files = fileSystem.EnumerateFilesRecursively(currentDirectory, sourceExtensions).ToList();

            foreach (var path in variables.GetStrings(ActionVariables.AdditionalPaths).Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var pathFiles = fileSystem.EnumerateFilesRecursively(path, sourceExtensions);
                files.AddRange(pathFiles);
            }

            return files;
        }
    }
}