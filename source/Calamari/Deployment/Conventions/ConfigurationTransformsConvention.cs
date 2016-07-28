using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.FileSystem;
using NuGet;

namespace Calamari.Deployment.Conventions
{
    public class ConfigurationTransformsConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IConfigurationTransformer configurationTransformer;
        private readonly ITransformFileLocator transformFileLocator;

        public ConfigurationTransformsConvention(ICalamariFileSystem fileSystem, IConfigurationTransformer configurationTransformer, ITransformFileLocator transformFileLocator)
        {
            this.fileSystem = fileSystem;
            this.configurationTransformer = configurationTransformer;
            this.transformFileLocator = transformFileLocator;
        }

        public void Install(RunningDeployment deployment)
        {
            var explicitTransforms = GetExplicitTransforms(deployment);
            var automaticTransforms = GetAutomaticTransforms(deployment);
            var sourceExtensions = GetSourceExtensions(deployment, explicitTransforms);           

            var allTransforms = explicitTransforms.Concat(automaticTransforms).ToList();
            var transformDefinitionsApplied = new List<XmlConfigTransformDefinition>();
            var transformFilesApplied = new HashSet<Tuple<string, string>>();
           
            foreach (var configFile in fileSystem.EnumerateFilesRecursively(deployment.CurrentDirectory, sourceExtensions.ToArray()))
            {
                ApplyTransformations(configFile, allTransforms, transformFilesApplied, transformDefinitionsApplied);
            }

            LogFailedTransforms(explicitTransforms.Except(transformDefinitionsApplied));
            deployment.Variables.SetStrings(SpecialVariables.AppliedXmlConfigTransforms, transformFilesApplied.Select(t => t.Item1), "|");
        }

        private static List<XmlConfigTransformDefinition> GetAutomaticTransforms(RunningDeployment deployment)
        {
            var result = new List<XmlConfigTransformDefinition>();
            if (deployment.Variables.GetFlag(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles))
            {
                result.Add(new XmlConfigTransformDefinition("Release"));

                var environment = deployment.Variables.Get(SpecialVariables.Environment.Name);
                if (!string.IsNullOrWhiteSpace(environment))
                {
                    result.Add(new XmlConfigTransformDefinition(environment));
                }
            }
            return result;
        }

        private static List<XmlConfigTransformDefinition> GetExplicitTransforms(RunningDeployment deployment)
        {
            var transforms = deployment.Variables.Get(SpecialVariables.Package.AdditionalXmlConfigurationTransforms);

            if (string.IsNullOrWhiteSpace(transforms))
                return new List<XmlConfigTransformDefinition>();

            return transforms
                .Split(',', '\r', '\n')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => new XmlConfigTransformDefinition(s))
                .ToList();
        }

        void ApplyTransformations(string sourceFile, IEnumerable<XmlConfigTransformDefinition> transformations, 
            ISet<Tuple<string, string>> transformFilesApplied,  IList<XmlConfigTransformDefinition> transformDefinitionsApplied)
        {
            foreach (var transformation in transformations)
            {
                if ((transformation.IsTransformWildcard && !sourceFile.EndsWith(GetFileName(transformation.SourcePattern), StringComparison.InvariantCultureIgnoreCase)))
                    continue;
                try
                {
                    ApplyTransformations(sourceFile, transformation, transformFilesApplied, transformDefinitionsApplied);
                }
                catch (Exception)
                {
                    Log.ErrorFormat("Could not transform the file '{0}' using the {1}pattern '{2}'.", sourceFile, transformation.IsTransformWildcard ? "wildcard " : "", transformation.TransformPattern);
                    throw;
                }
            }
        }

        void ApplyTransformations(string sourceFile, XmlConfigTransformDefinition transformation, 
            ISet<Tuple<string, string>> transformFilesApplied,  ICollection<XmlConfigTransformDefinition> transformDefinitionsApplied)
        {
            if (transformation == null)
                return;

            foreach (var transformFile in transformFileLocator.DetermineTransformFileNames(sourceFile, transformation))
            {
                var transformFiles = new Tuple<string, string>(transformFile, sourceFile);
                if (transformFilesApplied.Contains(transformFiles))
                    continue;

                Log.Info("Transforming '{0}' using '{1}'.", sourceFile, transformFile);
                configurationTransformer.PerformTransform(sourceFile, transformFile, sourceFile);

                transformFilesApplied.Add(transformFiles);
                transformDefinitionsApplied.Add(transformation);
            }
        }

        private static string[] GetSourceExtensions(RunningDeployment deployment, List<XmlConfigTransformDefinition> transformDefinitions)
        {
            var extensions = new HashSet<string>();

            if (deployment.Variables.GetFlag(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles))
            {
                extensions.Add("*.config");
            }

            extensions.AddRange(transformDefinitions
                .Where(transform => transform.Advanced)
                .Select(transform => "*" + Path.GetExtension(transform.SourcePattern))
                .Distinct());

            return extensions.ToArray();
        }

        void LogFailedTransforms(IEnumerable<XmlConfigTransformDefinition> configTransform)
        {
            foreach (var transform in configTransform.Select(trans => trans.ToString()).Distinct())
            {
                Log.VerboseFormat("The transform pattern \"{0}\" was not performed due to a missing file or overlapping rule.", transform);
            }
        }

        static string GetFileName(string path)
        {
            return Path.GetFileName(path) ?? string.Empty;
        }
    }
}
