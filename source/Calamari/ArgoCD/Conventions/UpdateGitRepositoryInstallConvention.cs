#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Git;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes;
using LibGit2Sharp;
using SharpCompress;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateGitRepositoryInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly RepositoryFactory repositoryFactory;
        readonly ILog log;

        public UpdateGitRepositoryInstallConvention(ICalamariFileSystem fileSystem, string repositoryParentDirectory, ILog log)
        {
            this.fileSystem = fileSystem;
            this.log = log;
            repositoryFactory = new RepositoryFactory(log, repositoryParentDirectory);
        }
        
        public void Install(RunningDeployment deployment)
        {
            Log.Info("Executing Commit To Git operation");
            var fileWriter = GetReferencedPackageFiles(deployment);

            var commitMessage = GenerateCommitMessage(deployment);
            var requiresPullRequest = FeatureToggle.ArgocdCreatePullRequestFeatureToggle.IsEnabled(deployment.Variables) && deployment.Variables.Get(SpecialVariables.Git.CommitMethod) == "PullRequest";
            
            var repositoryIndexes = deployment.Variables.GetIndexes(SpecialVariables.Git.Index);
            log.Info($"Found the following repository indicies '{repositoryIndexes.Join(",")}'");
            foreach (var repositoryIndex in repositoryIndexes)
            {
                Log.Info($"Writing files to repository for '{repositoryIndex}'");
                IGitConnection gitConnection = new VariableBackedGitConnection(deployment.Variables, repositoryIndex);
                var repository = repositoryFactory.CloneRepository(repositoryIndex, gitConnection);
                
                Log.Info($"Copying files into repository {gitConnection.Url}");
                var subFolder = GetSubFolderFor(repositoryIndex, deployment.Variables);
                Log.VerboseFormat("Copying files into subfolder '{0}'", subFolder);
                var filesAdded = fileWriter.ApplyFilesTo(repository.WorkingDirectory, subFolder);
                
                Log.Info("Staging files in repository");
                repository.StageFiles(filesAdded.ToArray());
                
                Log.Info("Commiting changes");
                if (repository.CommitChanges(commitMessage))
                {
                    Log.Info("Changes were commited, pushing to remote");
                    repository.PushChanges(requiresPullRequest, gitConnection.BranchName);    
                }
                else
                {
                    Log.Info("No changes were commited.");
                }
            }
        }

        FileWriter GetReferencedPackageFiles(RunningDeployment deployment)
        {
            
            var fileGlobs = deployment.Variables.GetPaths(SpecialVariables.Git.TemplateGlobs);
            log.Info($"Selecting files from package using '{string.Join(" ", fileGlobs)}'");
            var filesToApply = SelectFiles(deployment.CurrentDirectory, fileGlobs);
            
            log.Info($"Found {filesToApply.Length} files to apply");
            var fileWriter = new FileWriter(fileSystem, filesToApply);
            
            return fileWriter;
        }

        string GetSubFolderFor(string repositoryIndex, IVariables variables)
        {
            {
                var raw = variables.Get(SpecialVariables.Git.SubFolder(repositoryIndex), String.Empty) ?? String.Empty;
                if (raw.StartsWith("./"))
                {
                    return raw.Substring(2);
                }

                return raw;
            }
        }

        string GenerateCommitMessage(RunningDeployment deployment)
        {
            var summary = deployment.Variables.GetMandatoryVariable(SpecialVariables.Git.CommitMessageSummary);
            var description = deployment.Variables.Get(SpecialVariables.Git.CommitMessageDescription) ?? string.Empty;
            return description.Equals(string.Empty)
                ? summary
                : $"{summary}\n\n{description}";
        }
        
        PackageRelativeFile[] SelectFiles(string pathToExtractedPackage, List<string> fileGlobs)
        {
            return fileGlobs.SelectMany(glob => fileSystem.EnumerateFilesWithGlob(pathToExtractedPackage, glob))
                            .Select(absoluteFilepath =>
                                    {
#if NETCORE
                                        var relativePath = Path.GetRelativePath(pathToExtractedPackage, file);
#else
                                        var relativePath = absoluteFilepath.Substring(pathToExtractedPackage.Length + 1);
#endif
                                        return new PackageRelativeFile(absoluteFilepath, relativePath);
                                    })
                            .ToArray();
        }
    }
}