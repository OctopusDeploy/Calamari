using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Features.ConfigurationTransforms
{
    public class TransformFileLocator : ITransformFileLocator
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public TransformFileLocator(ICalamariFileSystem fileSystem, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public IEnumerable<string> DetermineTransformFileNames(string sourceFile, XmlConfigTransformDefinition transformation, bool diagnosticLoggingEnabled, string currentDirectory)
        {
            var defaultTransformFileName = DetermineTransformFileName(sourceFile, transformation, true);
            var transformFileName = DetermineTransformFileName(sourceFile, transformation, false);

            string fullTransformDirectoryPath;
            if (Path.IsPathRooted(transformFileName))
            {
                fullTransformDirectoryPath = Path.GetFullPath(GetDirectoryName(transformFileName));
            }
            else
            {
                var relativeTransformPath = fileSystem.GetRelativePath(sourceFile, transformFileName);
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
                    var sourceFileName = GetSourceFileName(sourceFile, transformation, transformFileName, transformFile, currentDirectory);

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

        private string GetSourceFileName(string sourceFile, XmlConfigTransformDefinition transformation,
            string transformFileName, string transformFile, string currentDirectory)
        {
            var sourcePattern = transformation.SourcePattern ?? "";
            if (Path.IsPathRooted(transformFileName) && sourcePattern.StartsWith("." + Path.DirectorySeparatorChar))
            {
                var path = fileSystem.GetRelativePath(currentDirectory, sourceFile);
                return "." + path.Substring(path.IndexOf(Path.DirectorySeparatorChar));
            }

            if (sourcePattern.Contains(Path.DirectorySeparatorChar))
                return fileSystem.GetRelativePath(transformFile, sourceFile)
                    .TrimStart('.', Path.DirectorySeparatorChar);

            return GetFileName(sourceFile);
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
            if (transformation.SourcePattern != null && transformation.SourcePattern.StartsWith("."))
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

        static bool DoesFileMatchWildcardPattern(string fileName, string? pattern)
        {
            var patternDirectory = GetDirectoryName(pattern);
            var regexBuilder = new StringBuilder();
            regexBuilder.Append(Regex.Escape(patternDirectory))
                .Append(string.IsNullOrEmpty(patternDirectory) ? string.Empty : Regex.Escape(Path.DirectorySeparatorChar.ToString()))
                .Append(".*?").Append(Regex.Escape("."))
                .Append(Regex.Escape(Path.GetFileName(pattern)?.TrimStart('.') ?? string.Empty));

            return Regex.IsMatch(fileName, regexBuilder.ToString(), RegexOptions.IgnoreCase);
        }

        [return: NotNullIfNotNull("path")]
        static string? GetDirectoryName(string? path)
        {
            return Path.GetDirectoryName(path);
        }

        [return: NotNullIfNotNull("path")]
        static string? GetFileName(string? path)
        {
            return Path.GetFileName(path);
        }

        static string GetTransformationFileDirectory(string sourceFile, XmlConfigTransformDefinition transformation)
        {
            var sourceDirectory = GetDirectoryName(sourceFile);
            if (transformation.SourcePattern == null || !transformation.SourcePattern.Contains(Path.DirectorySeparatorChar))
                return sourceDirectory;

            var sourcePattern = transformation.SourcePattern;
            var sourcePatternPath = sourcePattern.Substring(0, sourcePattern.LastIndexOf(Path.DirectorySeparatorChar));

            if (sourceDirectory.EndsWith(sourcePatternPath, StringComparison.OrdinalIgnoreCase))
                return sourceDirectory.Substring(0, sourceDirectory.Length - sourcePatternPath.Length);

            return sourceDirectory;
        }
    }
}