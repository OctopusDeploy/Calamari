using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.FileSystem;

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

            var transformDefinitions = GetTransformDefinitions(deployment.Variables.Get(SpecialVariables.Package.AdditionalXmlConfigurationTransforms));

            var sourceExtensions = new HashSet<string>(
                  transformDefinitions
                    .Where(transform => transform.Advanced)
                    .Select(transform => "*" + Path.GetExtension(transform.SourcePattern))
                    .Distinct()
                );

            if (deployment.Variables.GetFlag(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles))
            {
                sourceExtensions.Add("*.config");
                transformDefinitions.Add(new XmlConfigTransformDefinition("Release"));

                var environment = deployment.Variables.Get(SpecialVariables.Environment.Name);
                if (!string.IsNullOrWhiteSpace(environment))
                {
                    transformDefinitions.Add(new XmlConfigTransformDefinition(environment));
                }
            }

            var transformsRun = new HashSet<string>();
            foreach (var configFile in fileSystem.EnumerateFilesRecursively(deployment.CurrentDirectory,
                sourceExtensions.ToArray()))
            {
                ApplyTransformations(configFile, transformDefinitions, transformsRun);

            }

            deployment.Variables.SetStrings(SpecialVariables.AppliedXmlConfigTransforms, transformsRun, "|");
        }

        void ApplyTransformations(string sourceFile, IEnumerable<XmlConfigTransformDefinition> transformations,
            HashSet<string> alreadyRun)
        {

            foreach (var transformation in transformations)
            {
                if (transformation.Advanced && !transformation.Wildcard && !string.Equals(transformation.SourcePattern, Path.GetFileName(sourceFile), StringComparison.InvariantCultureIgnoreCase))
                    continue;

                if ((transformation.Wildcard && !sourceFile.EndsWith(transformation.SourcePattern, StringComparison.InvariantCultureIgnoreCase)))
                    continue;

                foreach (var transformFile in DetermineTransformFileNames(sourceFile, transformation))
                {
                    if (!fileSystem.FileExists(transformFile))
                        continue;

                    if (string.Equals(sourceFile, transformFile, StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    if (alreadyRun.Contains(transformFile))
                        continue;

                    Log.Info("Transforming '{0}' using '{1}'.", sourceFile, transformFile);
                    configurationTransformer.PerformTransform(sourceFile, transformFile, sourceFile);
                    alreadyRun.Add(transformFile);
                }

            }
        }

        private static List<XmlConfigTransformDefinition> GetTransformDefinitions(string transforms)
        {
            if (string.IsNullOrWhiteSpace(transforms))
                return new List<XmlConfigTransformDefinition>();

            return transforms
                .Split(',', '\r', '\n')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => new XmlConfigTransformDefinition(s))
                .ToList();
        }

        private IEnumerable<string> DetermineTransformFileNames(string sourceFile, XmlConfigTransformDefinition transformation)
        {
            // The reason we use fileSystem.EnumerateFiles here is to get the actual file-names from the physical file-system.
            // This prevents any issues with mis-matched casing in transform specifications.
            return fileSystem.EnumerateFiles(Path.GetDirectoryName(sourceFile),
               Path.GetFileName(DetermineTransformFileName(sourceFile, transformation, true)),
               Path.GetFileName(DetermineTransformFileName(sourceFile, transformation, false))
              );
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