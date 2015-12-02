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

        public ConfigurationTransformsConvention(ICalamariFileSystem fileSystem, IConfigurationTransformer configurationTransformer)
        {
            this.fileSystem = fileSystem;
            this.configurationTransformer = configurationTransformer;
        }

        public void Install(RunningDeployment deployment)
        {

            var explicitTransforms = GetExplicitTransforms(deployment);
            var automaticTransforms = GetAutomaticTransforms(deployment);
            var sourceExtensions = GetSourceExtensions(deployment, explicitTransforms);           


            var transformsRun = new Dictionary<string, XmlConfigTransformDefinition>();
            var allTransforms = explicitTransforms.Concat(automaticTransforms).ToList();
            foreach (var configFile in fileSystem.EnumerateFilesRecursively(deployment.CurrentDirectory, sourceExtensions))
            {
                ApplyTransformations(configFile, allTransforms, transformsRun);
            }


            LogFailedTransforms(explicitTransforms.Except(transformsRun.Values));

            deployment.Variables.SetStrings(SpecialVariables.AppliedXmlConfigTransforms, transformsRun.Keys, "|");
        }

        private List<XmlConfigTransformDefinition> GetAutomaticTransforms(RunningDeployment deployment)
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
        
        void ApplyTransformations(string sourceFile, IEnumerable<XmlConfigTransformDefinition> transformations, Dictionary<string, XmlConfigTransformDefinition> alreadyRun)
        {
            foreach (var transformation in transformations)
            {
                if (transformation.Advanced && !transformation.Wildcard && !string.Equals(transformation.SourcePattern, Path.GetFileName(sourceFile), StringComparison.InvariantCultureIgnoreCase))
                    continue;

                if ((transformation.Wildcard && !sourceFile.EndsWith(transformation.SourcePattern, StringComparison.InvariantCultureIgnoreCase)))
                    continue;

                try
                {
                    ApplyTransformations(sourceFile, transformation, alreadyRun);
                }
                catch (Exception)
                {
                    Log.ErrorFormat("Could not transform the file '{0}' using the {1}pattern '{2}'.", sourceFile, transformation.Wildcard ? "wildcard " : "", transformation.TransformPattern);
                    throw;
                }
            }
        }

        void ApplyTransformations(string sourceFile, XmlConfigTransformDefinition transformation, Dictionary<string, XmlConfigTransformDefinition> alreadyRun)
        {
            foreach (var transformFile in DetermineTransformFileNames(sourceFile, transformation))
            {
                if (!fileSystem.FileExists(transformFile))
                    continue;

                if (string.Equals(sourceFile, transformFile, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                if (alreadyRun.ContainsKey(transformFile))
                    continue;

                Log.Info("Transforming '{0}' using '{1}'.", sourceFile, transformFile);
                configurationTransformer.PerformTransform(sourceFile, transformFile, sourceFile);
                alreadyRun.Add(transformFile, transformation);
            }
        }

        private IEnumerable<string> DetermineTransformFileNames(string sourceFile, XmlConfigTransformDefinition transformation)
        {
            // The reason we use fileSystem.EnumerateFiles here is to get the actual file-names from the physical file-system.
            // This prevents any issues with mis-matched casing in transform specifications.
            return fileSystem.EnumerateFiles(Path.GetDirectoryName(sourceFile),
               GetRelativePathToTransformFile(sourceFile, DetermineTransformFileName(sourceFile, transformation, true)),
               GetRelativePathToTransformFile(sourceFile, DetermineTransformFileName(sourceFile, transformation, false))
            );
        }

        private static string GetRelativePathToTransformFile(string sourceFile, string transformFile)
        {
            return transformFile
                .Replace(Path.GetDirectoryName(sourceFile) ?? string.Empty, "")
                .TrimStart(Path.DirectorySeparatorChar);
        }

        private static string DetermineTransformFileName(string sourceFile, XmlConfigTransformDefinition transformation, bool defaultExtension)
        {
            var tp = transformation.TransformPattern;
            if (defaultExtension && !tp.EndsWith(".config"))
                tp += ".config";

            if (transformation.Advanced && transformation.Wildcard)
            {
                var baseFileName = sourceFile.Replace(transformation.SourcePattern, "");
                return Path.ChangeExtension(baseFileName, tp);
            }

            if (transformation.Advanced && !transformation.Wildcard)
                return Path.Combine(Path.GetDirectoryName(sourceFile), tp);

            return Path.ChangeExtension(sourceFile, tp);
        }


    }
}