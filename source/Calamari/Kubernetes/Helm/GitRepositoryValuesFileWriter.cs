using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions.DependencyVariables;

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
        
        
        public static IEnumerable<string> FindGitDependencyValuesFiles(RunningDeployment deployment,
                                                                       ICalamariFileSystem fileSystem,
                                                                       ILog log,
                                                                       string gitDependencyName,
                                                                       string valuesFilePaths)
        {
            
            if (string.IsNullOrWhiteSpace(gitDependencyName))
            {
                log.Verbose("Sourcing secondary values files from primary git dependency is not supported.");
                return null;
            }
            
            var variables = deployment.Variables;
            var gitDependencyNames = variables.GetIndexes(Deployment.SpecialVariables.GitResources.GitResourceCollection);
            if (!gitDependencyNames.Contains(gitDependencyName))
            {
                return null;
            }

            var valuesPaths = valuesFilePaths?
                              .Split('\r', '\n')
                              .Where(x => !string.IsNullOrWhiteSpace(x))
                              .Select(x => x.Trim())
                              .ToList();
            
            if (valuesPaths == null || !valuesPaths.Any())
                return null;

            var filenames = new List<string>();
            var errors = new List<string>();

            var sanitizedPackageReferenceName = fileSystem.RemoveInvalidFileNameChars(gitDependencyName);

            foreach (var valuePath in valuesPaths)
            {
                var relativePath = Path.Combine(sanitizedPackageReferenceName, valuePath);
                var currentFiles = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, relativePath).ToList();

                if (!currentFiles.Any())
                {
                    errors.Add($"Unable to find file `{valuePath}` for git repository {gitDependencyName}");
                }

                foreach (var file in currentFiles)
                {
                    var relative = file.Substring(Path.Combine(deployment.CurrentDirectory, sanitizedPackageReferenceName).Length);
                    log.Info($"Including values file `{relative}` from git repository {gitDependencyName}");
                    filenames.Add(file);
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