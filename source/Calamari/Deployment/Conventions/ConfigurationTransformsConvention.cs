using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

            foreach (var transformFile in DetermineTransformFileNames(sourceFile, transformation))
            {
                var sourceFileName = (transformation?.SourcePattern?.Contains(Path.DirectorySeparatorChar) ?? false)
                    ? fileSystem.GetRelativePath(transformFile, sourceFile).TrimStart('.',Path.DirectorySeparatorChar)
                    : GetFileName(sourceFile);

                if (transformation.Advanced && !transformation.IsSourceWildcard && !string.Equals(transformation.SourcePattern, sourceFileName, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                if (transformation.Advanced && transformation.IsSourceWildcard && !DoesFileMatchWildcardPattern(sourceFileName, transformation.SourcePattern))
                    continue;

                if (!fileSystem.FileExists(transformFile))
                    continue;

                if (string.Equals(sourceFile, transformFile, StringComparison.InvariantCultureIgnoreCase))
                    continue;

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

        private IEnumerable<string> DetermineTransformFileNames(string sourceFile, XmlConfigTransformDefinition transformation)
        {
            var defaultTransformFileName = DetermineTransformFileName(sourceFile, transformation, true);
            var transformFileName = DetermineTransformFileName(sourceFile, transformation, false);

            var relativeTransformPath = fileSystem.GetRelativePath(sourceFile, transformFileName);
            var fullTransformPath = Path.GetFullPath(Path.Combine(GetDirectoryName(sourceFile), GetDirectoryName(relativeTransformPath)));

            if (!fileSystem.DirectoryExists(fullTransformPath))
                return Enumerable.Empty<string>();

            // The reason we use fileSystem.EnumerateFiles here is to get the actual file-names from the physical file-system.
            // This prevents any issues with mis-matched casing in transform specifications.
            return fileSystem.EnumerateFiles(fullTransformPath,
               GetFileName(defaultTransformFileName),
               GetFileName(transformFileName)
            );
        }

        private static string DetermineTransformFileName(string sourceFile, XmlConfigTransformDefinition transformation, bool defaultExtension)
        {
            var tp = transformation.TransformPattern;
            if (defaultExtension && !tp.EndsWith(".config"))
                tp += ".config";

            if (transformation.Advanced && transformation.IsTransformWildcard && transformation.IsSourceWildcard)
            {
                return DetermineWildcardTransformFileName(sourceFile, transformation, tp);
            }

            if (transformation.Advanced && transformation.IsTransformWildcard && !transformation.IsSourceWildcard)
            {
                var transformDirectory = GetTransformationFileDirectory(sourceFile, transformation);
                return Path.Combine(transformDirectory, GetDirectoryName(tp), "*." + GetFileName(tp).TrimStart('.'));
            }

            if (transformation.Advanced && !transformation.IsTransformWildcard)
            {
                var transformDirectory = GetTransformationFileDirectory(sourceFile, transformation);
                return Path.Combine(transformDirectory, tp);
            }

            return Path.ChangeExtension(sourceFile, tp);
        }

        static string GetDirectoryName(string path)
        {
            return Path.GetDirectoryName(path) ?? string.Empty;
        }

        static string GetFileName(string path)
        {
            return Path.GetFileName(path) ?? string.Empty;
        }

        static string DetermineWildcardTransformFileName(string sourceFile, XmlConfigTransformDefinition transformation, string transformPattern)
        {
            var sourcePatternWithoutPrefix = GetFileName(transformation.SourcePattern);
            if (transformation.SourcePattern.StartsWith("."))
            {
                sourcePatternWithoutPrefix = transformation.SourcePattern.Remove(0, 1);
            }

            var transformDirectory = GetTransformationFileDirectory(sourceFile, transformation);
            var baseFileName = transformation.IsSourceWildcard ?
                GetFileName(sourceFile).Replace(sourcePatternWithoutPrefix, "")
                : GetFileName(sourceFile);
            var baseTransformPath = Path.Combine(transformDirectory, GetDirectoryName(transformPattern), baseFileName);

            return Path.ChangeExtension(baseTransformPath, GetFileName(transformPattern));
        }

        static string GetTransformationFileDirectory(string sourceFile, XmlConfigTransformDefinition transformation)
        {
            var sourceDirectory = GetDirectoryName(sourceFile);
            if (!transformation.SourcePattern.Contains(Path.DirectorySeparatorChar))
                return sourceDirectory;

            var sourcePattern = transformation.SourcePattern;
            var sourcePatternPath = sourcePattern.Substring(0, sourcePattern.LastIndexOf(Path.DirectorySeparatorChar));
            return sourceDirectory.Replace(sourcePatternPath, string.Empty);
        }

        static bool DoesFileMatchWildcardPattern(string fileName, string pattern)
        {
            var patternDirectory = GetDirectoryName(pattern);
            var regexBuilder = new StringBuilder();
            regexBuilder.Append(Regex.Escape(patternDirectory))
                .Append(string.IsNullOrEmpty(patternDirectory) ? string.Empty : Regex.Escape(Path.DirectorySeparatorChar.ToString()))
                .Append(".*?").Append(Regex.Escape("."))
                .Append(Regex.Escape(Path.GetFileName(pattern)?.TrimStart('.') ?? string.Empty));

            return Regex.IsMatch(fileName, regexBuilder.ToString());
        }
    }
}
