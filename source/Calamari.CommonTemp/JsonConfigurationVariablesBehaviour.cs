using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.CommonTemp
{
    internal class JsonConfigurationVariablesBehaviour : IBehaviour
    {
        readonly IJsonConfigurationVariableReplacer jsonConfigurationVariableReplacer;
        readonly ICalamariFileSystem fileSystem;

        public JsonConfigurationVariablesBehaviour(IJsonConfigurationVariableReplacer jsonConfigurationVariableReplacer, ICalamariFileSystem fileSystem)
        {
            this.jsonConfigurationVariableReplacer = jsonConfigurationVariableReplacer;
            this.fileSystem = fileSystem;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return context.Variables.GetFlag(KnownVariables.Package.JsonConfigurationVariablesEnabled);
        }

        public Task Execute(RunningDeployment context)
        {
            foreach (var target in context.Variables.GetPaths(KnownVariables.Package.JsonConfigurationVariablesTargets))
            {
                if (fileSystem.DirectoryExists(target))
                {
                    Log.Warn($"Skipping JSON variable replacement on '{target}' because it is a directory.");
                    continue;
                }

                var matchingFiles = MatchingFiles(context, target);

                if (!matchingFiles.Any())
                {
                    Log.Warn($"No files were found that match the replacement target pattern '{target}'");
                    continue;
                }

                foreach (var file in matchingFiles)
                {
                    Log.Info($"Performing JSON variable replacement on '{file}'");
                    jsonConfigurationVariableReplacer.ModifyJsonFile(file, context.Variables);
                }
            }

            return this.CompletedTask();
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