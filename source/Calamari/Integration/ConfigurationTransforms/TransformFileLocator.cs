using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Integration.FileSystem;

namespace Calamari.Integration.ConfigurationTransforms
{
    public class TransformFileLocator : ITransformFileLocator
    {
        private readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public TransformFileLocator(ICalamariFileSystem fileSystem, ILog log = null)
        {
            this.fileSystem = fileSystem;
            this.log = log ?? new LogWrapper();
        }

        public IEnumerable<string> DetermineTransformFileNames(string sourceFile, XmlConfigTransformDefinition transformation, bool diagnosticLoggingEnabled)
        {
            var defaultTransformFileName = DetermineTransformFileName(sourceFile, transformation, true);
            var transformFileName = DetermineTransformFileName(sourceFile, transformation, false);

            string fullTransformDirectoryPath;
            string fullTransformFilePath;
            if (Path.IsPathRooted(transformFileName))
            {
                if (transformation.IsTransformWildcard)
                    fullTransformFilePath = transformFileName;
                else
                    fullTransformFilePath = Path.GetFullPath(transformFileName);
                fullTransformDirectoryPath = Path.GetFullPath(GetDirectoryName(fullTransformFilePath));
            }
            else
            {
                var relativeTransformPath = fileSystem.GetRelativePath(sourceFile, transformFileName);
                if (transformation.IsTransformWildcard)
                    fullTransformFilePath = relativeTransformPath;
                else
                    fullTransformFilePath = Path.GetFullPath(relativeTransformPath);
                fullTransformDirectoryPath = Path.GetFullPath(Path.Combine(GetDirectoryName(sourceFile), GetDirectoryName(relativeTransformPath)));
            }
            
            if (!fileSystem.DirectoryExists(fullTransformDirectoryPath))
            {
                if (diagnosticLoggingEnabled)
                    log.Verbose($" - Skipping as transform folder \'{fullTransformDirectoryPath}\' does not exist");
                yield break;
            }

            // The reason we use fileSystem.EnumerateFiles here is to get the actual file-names from the physical file-system.
            // This prevents any issues with mis-matched casing in transform specifications.
            var enumerateFiles = fileSystem.EnumerateFiles(fullTransformDirectoryPath, GetFileName(defaultTransformFileName), GetFileName(transformFileName)).Distinct().ToArray();
            if (enumerateFiles.Any())
            {
                foreach (var transformFile in enumerateFiles)
                {
                    var sourceFileName = (transformation.SourcePattern?.Contains(Path.DirectorySeparatorChar) ?? false)
                        ? fileSystem.GetRelativePath(transformFile, sourceFile)
                            .TrimStart('.', Path.DirectorySeparatorChar)
                        : GetFileName(sourceFile);

                    if (transformation.Advanced && !transformation.IsSourceWildcard &&
                        !string.Equals(transformation.SourcePattern, sourceFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (diagnosticLoggingEnabled)
                            log.Verbose($" - Skipping as file name \'{sourceFileName}\' does not match the target pattern \'{transformation.SourcePattern}\'");
                        continue;
                    }

                    if (transformation.Advanced && transformation.IsSourceWildcard &&
                        !DoesFileMatchWildcardPattern(sourceFileName, transformation.SourcePattern))
                    {
                        if (diagnosticLoggingEnabled)
                            log.Verbose($" - Skipping as file name \'{sourceFileName}\' does not match the wildcard target pattern \'{transformation.SourcePattern}\'");
                        continue;
                    }

                    if (!fileSystem.FileExists(transformFile))
                    {
                        if (diagnosticLoggingEnabled)
                            log.Verbose($" - Skipping as transform \'{transformFile}\' does not exist");
                        continue;
                    }

                    if (string.Equals(sourceFile, transformFile, StringComparison.OrdinalIgnoreCase))
                    {
                        if (diagnosticLoggingEnabled)
                            log.Verbose($" - Skipping as target \'{sourceFile}\' is the same as transform \'{transformFile}\'");
                        continue;
                    }

                    yield return transformFile;
                }
            }
            else if (diagnosticLoggingEnabled)
            {
                if (GetFileName(defaultTransformFileName) == GetFileName(transformFileName))
                    log.Verbose($" - skipping as transform \'{GetFileName(defaultTransformFileName)}\' could not be found in \'{fullTransformDirectoryPath}\'");
                else
                    log.Verbose($" - skipping as neither transform \'{GetFileName(defaultTransformFileName)}\' nor transform \'{GetFileName(transformFileName)}\' could be found in \'{fullTransformDirectoryPath}\'");
            }
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

        static string GetDirectoryName(string path)
        {
            return Path.GetDirectoryName(path) ?? string.Empty;
        }

        static string GetFileName(string path)
        {
            return Path.GetFileName(path) ?? string.Empty;
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
    }
}