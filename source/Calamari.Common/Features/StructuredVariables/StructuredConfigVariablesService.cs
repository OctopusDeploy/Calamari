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
        readonly IStructuredConfigVariableReplacer structuredConfigVariableReplacer;
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;

        public StructuredConfigVariablesService(
            IStructuredConfigVariableReplacer structuredConfigVariableReplacer,
            ICalamariFileSystem fileSystem,
            ILog log)
        {
            this.structuredConfigVariableReplacer = structuredConfigVariableReplacer;
            this.fileSystem = fileSystem;
            this.log = log;
        }

        public void ReplaceVariables(RunningDeployment deployment)
        {
            foreach (var target in deployment.Variables.GetPaths(PackageVariables.JsonConfigurationVariablesTargets))
            {
                if (fileSystem.DirectoryExists(target))
                {
                    log.Warn($"Skipping JSON variable replacement on '{target}' because it is a directory.");
                    continue;
                }

                var matchingFiles = MatchingFiles(deployment, target);

                if (!matchingFiles.Any())
                {
                    log.Warn($"No files were found that match the replacement target pattern '{target}'");
                    continue;
                }

                foreach (var file in matchingFiles)
                {
                    log.Info($"Performing JSON variable replacement on '{file}'");
                    structuredConfigVariableReplacer.ModifyFile(file, deployment.Variables);
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
    }
}