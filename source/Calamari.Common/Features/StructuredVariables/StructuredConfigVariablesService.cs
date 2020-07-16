using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.Common.Features.StructuredVariables
{
    public interface IStructuredConfigVariablesService
    {
        void DoStructuredVariableReplacement(RunningDeployment deployment);
        
        void DoJsonVariableReplacement(RunningDeployment deployment);
    }

    public class StructuredConfigVariablesService : IStructuredConfigVariablesService
    {
        readonly IFileFormatVariableReplacer[] replacers;
        readonly ICalamariFileSystem fileSystem;

        public StructuredConfigVariablesService(
            ICalamariFileSystem fileSystem,
            IEnumerable<IFileFormatVariableReplacer> replacers
        )
        {
            this.replacers = replacers.ToArray();
            this.fileSystem = fileSystem;
        }

        public void DoJsonVariableReplacement(RunningDeployment deployment)
        {
            var paths = deployment.Variables.GetPaths(PackageVariables.JsonConfigurationVariablesTargets);

            foreach (var target in paths)
            {
                if (fileSystem.DirectoryExists(target))
                {
                    Log.Warn($"Skipping JSON variable replacement on '{target}' because it is a directory.");
                    continue;
                }

                var matchingFiles = GetMatchingFiles(deployment, target);

                if (!matchingFiles.Any())
                {
                    Log.Warn($"No files were found that match the replacement target pattern '{target}'");
                    continue;
                }

                foreach (var file in matchingFiles)
                {
                    Log.Info($"Performing JSON variable replacement on '{file}'");
                    DoReplacement(deployment.Variables, file, StructuredConfigVariablesFileFormats.Json);
                }
            }
        }

        public void DoStructuredVariableReplacement(RunningDeployment deployment)
        {
            var targetsAsJson = deployment.Variables.Get(ActionVariables.StructuredConfigurationVariablesTargets);
            var targets = JsonConvert.DeserializeObject<StructuredConfigVariablesModel[]>(targetsAsJson);
            
            foreach (var targetModel in targets)
            {
                var evaluatedTarget = deployment.Variables.Evaluate(targetModel.Target);
                var splitTarget = evaluatedTarget.Split('\r', '\n')
                    .Select(v => v.Trim())
                    .Where(v => v != "");

                foreach (var target in splitTarget)
                {
                    if (fileSystem.DirectoryExists(target))
                    {
                        Log.Warn($"Skipping JSON variable replacement on '{target}' because it is a directory.");
                        continue;
                    }

                    var matchingFiles = GetMatchingFiles(deployment, target);

                    if (!matchingFiles.Any())
                    {
                        Log.Warn($"No files were found that match the replacement target pattern '{target}'");
                        continue;
                    }

                    foreach (var file in matchingFiles)
                    {
                        Log.Info($"Performing structured variable replacement on '{file}'");
                        DoReplacement(deployment.Variables, file, targetModel.Format);
                    }
                }
            }
        }

        void DoReplacement(IVariables variables, string filePath, string format)
        {
            var replacer = replacers.FirstOrDefault(r => r.SupportedFormat.Equals(format, StringComparison.OrdinalIgnoreCase));
            if (replacer == null)
            {
                // File format not supported.
                // This could either indicate bad data received from Octopus Server, or
                // an old version of Calamari.

                var supportedFormats = replacers.Select(r => r.SupportedFormat);
                var supportedFormatsAsString = string.Join(", ", supportedFormats);

                throw new NotSupportedException(
                    $"The structured config file format '{format}' is not supported by " +
                    $"this version of Calamari. The supported formats are: {supportedFormatsAsString}"
                );
            }

            replacer.ModifyFile(filePath, variables);
        }  
        
        List<string> GetMatchingFiles(RunningDeployment deployment, string target)
        {
            var files = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, target)
                .Select(Path.GetFullPath);

            var additionalPaths = deployment.Variables
                .GetStrings(ActionVariables.AdditionalPaths)
                .Where(s => !string.IsNullOrWhiteSpace(s));

            var additionalFiles = additionalPaths
                .SelectMany(path => fileSystem.EnumerateFilesWithGlob(path, target))
                .Select(Path.GetFullPath);

            return files
                .Concat(additionalFiles)
                .ToList();
        }
    }
}