﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Kubernetes.Helm
{
    public static class GitRepositoryValuesFileWriter
    {
        public static IEnumerable<string> FindChartValuesFiles(RunningDeployment deployment, ICalamariFileSystem fileSystem, ILog log, string valuesFilePaths)
            => FindGitDependencyValuesFiles(deployment,
                                      fileSystem,
                                      log,
                                      string.Empty,
                                      valuesFilePaths);
        
        
        public static IEnumerable<string> FindGitDependencyValuesFiles(RunningDeployment deployment,
                                                                       ICalamariFileSystem fileSystem,
                                                                       ILog log,
                                                                       string gitDependencyName,
                                                                       string valuesFilePaths)
        {
            var variables = deployment.Variables;
            var gitDependencyNames = variables.GetIndexes(Deployment.SpecialVariables.GitResources.GitResourceCollection);
            if (!gitDependencyNames.Contains(gitDependencyName))
            {
                log.Warn($"Failed to find variables for git resource {gitDependencyName}");
                return null;
            }


            var valuesPaths = HelmValuesFileUtils.SplitValuesFilePaths(valuesFilePaths);
            if (valuesPaths == null || !valuesPaths.Any())
                return null;

            var filenames = new List<string>();
            var errors = new List<string>();

            var sanitizedPackageReferenceName = fileSystem.RemoveInvalidFileNameChars(gitDependencyName);

            var repositoryUrl = variables.Get(Deployment.SpecialVariables.GitResources.RepositoryUrl(gitDependencyName));
            var commitHash = variables.Get(Deployment.SpecialVariables.GitResources.CommitHash(gitDependencyName));
            
            foreach (var valuePath in valuesPaths)
            {
                var relativePath = Path.Combine(sanitizedPackageReferenceName, valuePath);
                var currentFiles = fileSystem.EnumerateFilesWithGlob(deployment.CurrentDirectory, relativePath).ToList();

                if (!currentFiles.Any())
                {
                    errors.Add($"Unable to find file `{valuePath}` for git repository {repositoryUrl}, commit {commitHash}");
                }

                foreach (var file in currentFiles)
                {
                    var relative = file.Substring(Path.Combine(deployment.CurrentDirectory, sanitizedPackageReferenceName).Length);
                    log.Info($"Including values file `{relative}` from git repository {repositoryUrl}, commit {commitHash}");
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