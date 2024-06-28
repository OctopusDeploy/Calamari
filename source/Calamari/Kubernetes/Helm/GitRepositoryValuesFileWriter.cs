using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Kubernetes.Helm
{
    public static class GitRepositoryValuesFileWriter
    {
        public static IEnumerable<string> FindChartValuesFiles(RunningDeployment deployment, ICalamariFileSystem fileSystem, ILog log, string valuesFilePaths)
        {
            var valuesPaths = valuesFilePaths?
                              .Split('\r', '\n')
                              .Where(x => !string.IsNullOrWhiteSpace(x))
                              .Select(x => x.Trim())
                              .ToList();
            
            if (valuesPaths == null || !valuesPaths.Any())
                return null;
            
            var filenames = new List<string>();
            var errors = new List<string>();

            var hash = deployment.Variables.Get(KnownVariables.Action.GitResource.CommitHash);

            foreach (var valuePath in valuesPaths)
            {
                var currentFiles = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, valuePath).ToList();
                if (!currentFiles.Any())
                {
                    errors.Add($"Unable to find file `{valuePath}` in git repository, commit {hash}");
                }

                foreach (var file in currentFiles)
                {
                    var relative = file.Substring(deployment.CurrentDirectory.Length);
                    log.Info($"Including values file `{relative}` from git repository, commit {hash}");
                    filenames.AddRange(currentFiles);
                }
            }

            if (!filenames.Any() && errors.Any())
            {
                throw new CommandException(string.Join(Environment.NewLine, errors));
            }

            return filenames;
        }
    }
}