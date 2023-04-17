using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autofac;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.StructuredVariables
{
    /// <summary>
    /// Order matters, so we opt for explicit registration over scanning
    /// </summary>
    public class StructuredConfigVariablesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<JsonFormatVariableReplacer>().As<IFileFormatVariableReplacer>().WithPriority(1);
            builder.RegisterType<XmlFormatVariableReplacer>().As<IFileFormatVariableReplacer>().WithPriority(2);
            builder.RegisterType<YamlFormatVariableReplacer>().As<IFileFormatVariableReplacer>().WithPriority(3);
            builder.RegisterType<PropertiesFormatVariableReplacer>().As<IFileFormatVariableReplacer>().WithPriority(4);

            builder.RegisterPrioritisedList<IFileFormatVariableReplacer>();

            builder.RegisterType<StructuredConfigVariablesService>().As<IStructuredConfigVariablesService>();
        }
    }

    public interface IStructuredConfigVariablesService
    {
        void ReplaceVariables(string currentDirectory);
        void ReplaceVariables(string currentDirectory, List<string> targets);
    }

    public class StructuredConfigVariablesService : IStructuredConfigVariablesService
    {
        readonly IFileFormatVariableReplacer jsonReplacer;
        readonly IFileFormatVariableReplacer[] allReplacers;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public StructuredConfigVariablesService(
            PrioritisedList<IFileFormatVariableReplacer> replacers,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;

            allReplacers = replacers.ToArray();
            this.variables = variables;

            jsonReplacer = replacers.FirstOrDefault(r => r.FileFormatName == StructuredConfigVariablesFileFormats.Json)
                           ?? throw new Exception("No JSON replacer was supplied. A JSON replacer is required as a fallback.");
        }

        public void ReplaceVariables(string currentDirectory)
        {
            ReplaceVariables(currentDirectory,
                variables.GetPaths(ActionVariables.StructuredConfigurationVariablesTargets)
                         .SelectMany(v => v.Split(';')).ToList());
        }

        public void ReplaceVariables(string currentDirectory, List<string> targets)
        {
            var onlyPerformJsonReplacement = variables.GetFlag(ActionVariables.StructuredConfigurationFallbackFlag);

            foreach (var target in targets)
            {
                if (fileSystem.DirectoryExists(target))
                {
                    log.Warn($"Skipping structured variable replacement on '{target}' because it is a directory.");
                    continue;
                }

                var matchingFiles = MatchingFiles(currentDirectory, target);

                if (!matchingFiles.Any())
                {
                    log.Warn($"No files were found that match the replacement target pattern '{target}'");
                    continue;
                }

                foreach (var filePath in matchingFiles)
                {
                    var replacersToTry = GetReplacersToTryForFile(filePath, onlyPerformJsonReplacement).ToArray();

                    log.Verbose($"The registered replacers we will try, in order, are: " + string.Join(",", replacersToTry.Select(r => r.GetType().Name)));

                    DoReplacement(filePath, variables, replacersToTry);
                }
            }
        }

        List<string> MatchingFiles(string currentDirectory, string target)
        {
            var files = fileSystem.EnumerateFilesWithGlob(currentDirectory, target).Select(Path.GetFullPath).ToList();

            foreach (var path in variables.GetStrings(ActionVariables.AdditionalPaths).Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var pathFiles = fileSystem.EnumerateFilesWithGlob(path, target).Select(Path.GetFullPath);
                files.AddRange(pathFiles);
            }

            return files;
        }

        IEnumerable<IFileFormatVariableReplacer> GetReplacersToTryForFile(string filePath, bool onlyPerformJsonReplacement)
        {
            return GetParsersWhenOnlyPerformingJsonReplacement(filePath, onlyPerformJsonReplacement)
                   ?? GetParsersBasedOnFileName(filePath)
                   ?? GetAllParsers(filePath);
        }

        IEnumerable<IFileFormatVariableReplacer>? GetParsersWhenOnlyPerformingJsonReplacement(string filePath, bool onlyPerformJsonReplacement)
        {
            if (!onlyPerformJsonReplacement)
            {
                return null;
            }

            log.Verbose($"The {ActionVariables.StructuredConfigurationFallbackFlag} flag is set. The file at "
                        + $"{filePath} will be parsed as JSON.");
            return new[] { jsonReplacer };
        }

        IEnumerable<IFileFormatVariableReplacer>? GetParsersBasedOnFileName(string filePath)
        {
            var guessedParserBasedOnFileName = allReplacers.FirstOrDefault(r => r.IsBestReplacerForFileName(filePath));
            if (guessedParserBasedOnFileName == null)
            {
                return null;
            }

            var guessedParserMessage = $"The file at {filePath} matches a known filename pattern, and will be "
                                       + $"treated as {guessedParserBasedOnFileName.FileFormatName}.";
            if (guessedParserBasedOnFileName == jsonReplacer)
            {
                log.Verbose(guessedParserMessage);
                return new[] { jsonReplacer };
            }

            log.Verbose($"${guessedParserMessage} The file will be tried as {jsonReplacer.FileFormatName} first for backwards compatibility.");
            return new[]
            {
                jsonReplacer,
                guessedParserBasedOnFileName
            };
        }

        IEnumerable<IFileFormatVariableReplacer> GetAllParsers(string filePath)
        {
            log.Verbose($"The file at {filePath} does not match any known filename patterns. "
                        + "The file will be tried as multiple formats and will be treated as the first format that can be successfully parsed.");

            // Order so that the json replacer comes first
            yield return jsonReplacer;
            foreach (var replacer in allReplacers.Except(new[] { jsonReplacer }))
            {
                yield return replacer;
            }
        }

        void DoReplacement(string filePath, IVariables variables, IFileFormatVariableReplacer[] replacersToTry)
        {
            for (var r = 0; r < replacersToTry.Length; r++)
            {
                var replacer = replacersToTry[r];
                var format = replacer.FileFormatName;
                var isLastParserToTry = r == replacersToTry.Length - 1;

                try
                {
                    log.Verbose($"Attempting structured variable replacement on file {filePath} with format {format}");
                    replacer.ModifyFile(filePath, variables);
                    log.Info($"Structured variable replacement succeeded on file {filePath} with format {format}");
                    return;
                }
                catch (StructuredConfigFileParseException parseException) when (!isLastParserToTry)
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