using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
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
            var onlyPerformJsonReplacement = deployment.Variables.GetFlag(ActionVariables.StructuredConfigurationFallbackFlag);	
            
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
                    var replacersToTry = GetReplacersToTryForFile(filePath, onlyPerformJsonReplacement);
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

        IFileFormatVariableReplacer[] GetReplacersToTryForFile(string filePath, bool onlyPerformJsonReplacement)
        {
            if (onlyPerformJsonReplacement)
            {
                log.Verbose($"The {ActionVariables.StructuredConfigurationFallbackFlag} flag is set. The file at "
                            + $"{filePath} will be parsed as JSON.");

                return new []	
                {	
                    jsonReplacer	
                };	
            }

            var guessBasedOnFilePath = FindBestNonJsonReplacerForFilePath(filePath);
            if (guessBasedOnFilePath != null)
            {
                log.Verbose($"The file at {filePath} matches a known filename pattern, and will be "
                            + $"treated as {guessBasedOnFilePath.FileFormatName}. The file will be tried "
                            + $"as {jsonReplacer.FileFormatName} first for backwards compatibility.");

                return new []
                {
                    // For backwards compatibility, always try JSON first.
                    jsonReplacer,
                    guessBasedOnFilePath
                };
            }

            log.Verbose($"The file at {filePath} will be treated as JSON.");

            return new []
            {
                jsonReplacer
            };
        }

        IFileFormatVariableReplacer? FindBestNonJsonReplacerForFilePath(string filePath)
        {
            return allReplacers
                   .Where(r => r.FileFormatName != StructuredConfigVariablesFileFormats.Json)
                   .FirstOrDefault(r => r.IsBestReplacerForFileName(filePath));
        }

        void DoReplacement(string filePath, IVariables variables, IFileFormatVariableReplacer[] replacersToTry)
        {
            foreach (var (replacer, isLastReplacer) in replacersToTry.Select((replacer, ii) => (replacer,
                                                                                                ii == replacersToTry.Length - 1)))
            {
                var format = replacer.FileFormatName;
                try
                {
                    log.Verbose($"Attempting structured variable replacement on file {filePath} with format {format}");
                    replacer.ModifyFile(filePath, variables);
                    log.Info($"Structured variable replacement succeeded on file {filePath} with format {format}");
                    return;
                }
                catch (StructuredConfigFileParseException parseException) when (!isLastReplacer)
                {
                    log.Verbose($"The file at {filePath} couldn't be parsed as {format}: {parseException.Message}");
                }
                catch (StructuredConfigFileParseException parseException)
                {
                    var message = $"Structured variable replacement failed on file {filePath}. "
                                  + $"The file could not be parsed as {format}: {parseException.Message} "
                                  + "See verbose logs for more details.";
                    throw new Exception(message);
                }
            }
        }
    }
}