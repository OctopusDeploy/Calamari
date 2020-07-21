using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Common.Features.StructuredVariables
{
    public interface IStructuredConfigVariablesService
    {
        void ReplaceVariables(RunningDeployment deployment);
    }

    public class StructuredConfigVariablesService : IStructuredConfigVariablesService
    {
        public static readonly string FeatureToggleVariableName = "Octopus.Action.StructuredConfigurationFeatureFlag";

        readonly IFileFormatVariableReplacer[] replacers;
        readonly IFileFormatVariableReplacer jsonReplacer;
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public StructuredConfigVariablesService(
            IEnumerable<IFileFormatVariableReplacer> replacers,
            ICalamariFileSystem fileSystem,
            ILog log)
        {
            this.replacers = replacers.ToArray();
            this.fileSystem = fileSystem;
            this.log = log;

            jsonReplacer = this.replacers.FirstOrDefault(r => r.FileFormatName == StructuredConfigVariablesFileFormats.Json)
                           ?? throw new Exception("No JSON replacer was supplied. A JSON replacer is required as a fallback.");
        }

        public void ReplaceVariables(RunningDeployment deployment)
        {
            var targets = deployment.Variables.GetPaths(PackageVariables.JsonConfigurationVariablesTargets);
            var supportNonJsonReplacement = deployment.Variables.GetFlag(FeatureToggleVariableName);
            
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
                    var replacer = GetReplacerForFile(filePath, supportNonJsonReplacement);

                    log.Info($"Performing structured variable replacement on '{filePath}' with file format '{replacer.FileFormatName}'");
                    replacer.ModifyFile(filePath, deployment.Variables);
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

        IFileFormatVariableReplacer GetReplacerForFile(string filePath, bool supportNonJsonReplacement)
        {
            if (!supportNonJsonReplacement)
            {
                return jsonReplacer;
            }

            log.Info($"Feature toggle flag {FeatureToggleVariableName} detected. Trying replacers for all supported file formats.");
            
            // TODO: when we support explicit specification of file formats, handle that here.

            var replacer = replacers.FirstOrDefault(r => r.IsBestReplacerForFileName(filePath));
            if (replacer != null)
            {
                log.Info($"The config file at '{filePath}' is being handled as format '{replacer.FileFormatName}' because of the filename.");
                return replacer;
            }
            
            log.Info($"The config file at '{filePath}' is being handled as format '{StructuredConfigVariablesFileFormats.Json}' as a fallback.");
            return jsonReplacer;
        }
    }
}