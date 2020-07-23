using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.StructuredVariables
{
    public interface IStructuredConfigVariablesService
    {
        void ReplaceVariables(RunningDeployment deployment);
    }

    public class StructuredConfigVariablesService : IStructuredConfigVariablesService
    {
        readonly IFileFormatVariableReplacer jsonReplacer;
        readonly IFileFormatVariableReplacer[] allReplacers;
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public StructuredConfigVariablesService(
            IFileFormatVariableReplacer[] replacers,
            ICalamariFileSystem fileSystem,
            ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;

            allReplacers = replacers;
            
            jsonReplacer = replacers.FirstOrDefault(r => r.FileFormatName == StructuredConfigVariablesFileFormats.Json)
                           ?? throw new Exception("No JSON replacer was supplied. A JSON replacer is required as a fallback.");
        }

        public void ReplaceVariables(RunningDeployment deployment)
        {
            var targets = deployment.Variables.GetPaths(ActionVariables.StructuredConfigurationVariablesTargets);
            var supportNonJsonReplacement = deployment.Variables.GetFlag(ActionVariables.StructuredConfigurationFeatureFlag);
            
            foreach (var target in targets)
            {
                if (fileSystem.DirectoryExists(target))
                {
                    log.Warn($"Skipping structured variable replacement on '{target}' because it is a directory.");
                    continue;
                }

                var matchingFiles = MatchingFiles(deployment, target);

                if (!matchingFiles.Any())
                {
                    log.Warn($"No files were found that match the replacement target pattern '{target}'");
                    continue;
                }

                foreach (var filePath in matchingFiles)
                {
                    // TODO: once we allow users to specify a file format, pass it through here.
                    var replacersToTry = GetReplacersToTryForFile(filePath, null, supportNonJsonReplacement);
                    DoReplacement(filePath, deployment.Variables, replacersToTry);
                }
            }
        }

        List<string> MatchingFiles(RunningDeployment deployment, string target)
        {
            var files = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, target).Select(Path.GetFullPath).ToList();

            foreach (var path in deployment.Variables.GetStrings(ActionVariables.AdditionalPaths).Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var pathFiles = fileSystem.EnumerateFilesWithGlob(path, target).Select(Path.GetFullPath);
                files.AddRange(pathFiles);
            }

            return files;
        }

        IFileFormatVariableReplacer[] GetReplacersToTryForFile(string filePath, string? specifiedFileFormat, bool supportNonJsonReplacement)
        {
            if (!supportNonJsonReplacement)
            {
                return new []
                {
                    jsonReplacer
                };
            }

            log.Info($"Feature toggle flag {ActionVariables.StructuredConfigurationFeatureFlag} detected. Considering replacers for all supported file formats.");

            if (!string.IsNullOrWhiteSpace(specifiedFileFormat))
            {
                var specifiedReplacer = TryFindReplacerForFormat(specifiedFileFormat);

                return new []
                {
                    specifiedReplacer
                };
            }
            
            var guessBasedOnFilePath = FindBestNonJsonReplacerForFilePath(filePath);
            if (guessBasedOnFilePath != null)
            {
                return new []
                {
                    // For backwards compatibility, always try JSON first.
                    jsonReplacer,
                    guessBasedOnFilePath
                };
            }

            return new []
            {
                jsonReplacer
            };
        }

        IFileFormatVariableReplacer TryFindReplacerForFormat(string specifiedFileFormat)
        {
            var specifiedReplacer = allReplacers
                .FirstOrDefault(r => r.FileFormatName.Equals(specifiedFileFormat, StringComparison.OrdinalIgnoreCase));

            if (specifiedReplacer == null)
            {
                var availableFileFormats = string.Join(", ", allReplacers.Select(r => r.FileFormatName));
                var message = $"The file format specified ({specifiedFileFormat}) is invalid. "
                              + $"The available options are: {availableFileFormats}";
                throw new Exception(message);
            }

            return specifiedReplacer;
        }

        IFileFormatVariableReplacer? FindBestNonJsonReplacerForFilePath(string filePath)
        {
            return allReplacers
                   .Where(r => r.FileFormatName != StructuredConfigVariablesFileFormats.Json)
                   .FirstOrDefault(r => r.IsBestReplacerForFileName(filePath));
        }

        void DoReplacement(string filePath, IVariables variables, IFileFormatVariableReplacer[] replacersToTry)
        {
            var namesOfFormatsToTry = string.Join(", ", replacersToTry
                .Select(r => r.FileFormatName));

            log.Verbose($"Attempting structured variable replacement on file {filePath} with formats: {namesOfFormatsToTry}.");

            var attempts = new List<(string format, StructuredConfigFileParseException exception)>();
            
            foreach (var replacer in replacersToTry)
            {
                var format = replacer.FileFormatName;
                
                try
                {
                    log.Verbose($"Attempting structured variable replacement on file {filePath} with format '{replacer.FileFormatName}'");
                    replacer.ModifyFile(filePath, variables);
                    log.Info($"Structured variable replacement succeeded on file {filePath} with format '{replacer.FileFormatName}'");
                    return;
                }
                catch (StructuredConfigFileParseException parseException)
                {
                    attempts.Add((format, parseException));
                }
            }

            log.Warn($"Structured variable replacement failed on file {filePath}.");
            foreach (var attempt in attempts)
            {
                log.Warn($"Syntax error when parsing the file as {attempt.format}: {attempt.exception.Message}");
            }

            throw new Exception($"The file at {filePath} could not be parsed with any of the formats tried. See logs for more details.");
        }
    }
}