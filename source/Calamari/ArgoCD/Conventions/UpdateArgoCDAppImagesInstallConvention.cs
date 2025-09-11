#if NET
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppImagesInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly IGitHubPullRequestCreator pullRequestCreator;
        readonly ArgoCommitToGitConfigFactory argoCommitToGitConfigFactory;
        readonly ICommitMessageGenerator commitMessageGenerator;
        readonly ICustomPropertiesLoader customPropertiesLoader;
        int repositoryNumber = 1;

        public UpdateArgoCDAppImagesInstallConvention(ILog log,
                                                      IGitHubPullRequestCreator pullRequestCreator,
                                                      ICalamariFileSystem fileSystem,
                                                      ArgoCommitToGitConfigFactory argoCommitToGitConfigFactory,
                                                      ICommitMessageGenerator commitMessageGenerator,
                                                      ICustomPropertiesLoader customPropertiesLoader)
        {
            this.log = log;
            this.pullRequestCreator = pullRequestCreator;
            this.fileSystem = fileSystem;
            this.argoCommitToGitConfigFactory = argoCommitToGitConfigFactory;
            this.commitMessageGenerator = commitMessageGenerator;
            this.customPropertiesLoader = customPropertiesLoader;
        }

        public void Install(RunningDeployment deployment)
        {
            var actionConfig = argoCommitToGitConfigFactory.Create(deployment);
            var repositoryFactory = new RepositoryFactory(log, deployment.CurrentDirectory, pullRequestCreator);
            var argoProperties = customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>();
            
            log.Info($"Found {argoProperties.Applications.Length} Argo CD apps to update");
            var updatedApplications = new List<string>();
            var newImagesWritten = new HashSet<string>();
            var gitReposUpdated = new HashSet<string>();

            var outputWriter = new ArgoCDImageUpdateOutputWriter(log);
            
            foreach (var appToUpdate in argoProperties.Applications)
            {
                Log.Info($"Updating '{appToUpdate.Name}'");
                foreach (var source in appToUpdate.Sources.Where(s => s.SourceType.Equals("Directory", StringComparison.OrdinalIgnoreCase)))
                {
                    var repository = CreateRepository(source, argoProperties.Credentials, repositoryFactory);

                    var (updatedFiles, updatedImages) = UpdateKubernetesYaml(repository.WorkingDirectory, source.Path, appToUpdate.DefaultRegistry, actionConfig.PackageReferences);
                    
                    if (updatedImages.Count > 0)
                    {
                        PushToRemote(repository,
                                     new GitBranchName(source.TargetRevision),
                                     actionConfig,
                                     updatedFiles,
                                     updatedImages);
                        
                        newImagesWritten.UnionWith(updatedImages);
                        updatedApplications.Add(appToUpdate.Name);
                        gitReposUpdated.Add(source.Url);
                    }
                }
            }
            outputWriter.WriteImageUpdateOutput(Array.Empty<string>(),
                                                gitReposUpdated,
                                                updatedApplications,
                                                updatedApplications.Distinct(),
                                                newImagesWritten.Count
                                                );
        }

        RepositoryWrapper CreateRepository(ArgoCDApplicationSourceDto source, GitCredentialDto[] credentials, RepositoryFactory repositoryFactory)
        {
            var credentialsToUse = credentials.First(cred => cred.Url.Equals(source.Url, StringComparison.OrdinalIgnoreCase));
            var gitConnection = GitConnection.Create(source, credentialsToUse);
            var repoPath = repositoryNumber++.ToString(CultureInfo.InvariantCulture);
            Log.Info($"Writing files to git repository for '{gitConnection.Url}'");
            var repository = repositoryFactory.CloneRepository(repoPath, gitConnection);
            return repository;
        }

        (HashSet<string>, HashSet<string>) UpdateKubernetesYaml(string rootPath,
                                                                string subFolder,
                                                                string defaultRegistry,
                                                                List<ContainerImageReference> imagesToUpdate)
        {
            var absSubFolder = Path.Combine(rootPath, subFolder);
            var yamlFiles = FindYamlFiles(absSubFolder);

            var updatedFiles = new HashSet<string>();
            var updatedImages = new HashSet<string>();
            foreach (var file in yamlFiles)
            {
                var relativePath = Path.GetRelativePath(rootPath, file);
                log.Verbose($"Processing file {relativePath}.");
                var fileContent = fileSystem.ReadFile(file);

                var imageReplacer = new ContainerImageReplacer(fileContent, defaultRegistry);
                var imageReplacementResult = imageReplacer.UpdateImages(imagesToUpdate);

                if (imageReplacementResult.UpdatedImageReferences.Count > 0)
                {
                    fileSystem.OverwriteFile(file, imageReplacementResult.UpdatedContents);
                    updatedImages.UnionWith(imageReplacementResult.UpdatedImageReferences);
                    updatedFiles.Add(relativePath);
                    log.Verbose($"Updating file {file} with new image references.");
                    foreach (var change in imageReplacementResult.UpdatedImageReferences)
                    {
                        log.Verbose($"Updated image reference: {change}");
                    }
                }
                else
                {
                    log.Verbose($"No changes made to file {relativePath} as no image references were updated.");
                }
            }

            return (updatedFiles, updatedImages);
        }
        
        void PushToRemote(RepositoryWrapper repository,
                          GitBranchName branchName,
                          IGitCommitParameters commitParameters,
                          HashSet<string> updatedFiles,
                          HashSet<string> updatedImages)
        {
            Log.Info("Staging files in repository");
            repository.StageFiles(updatedFiles.ToArray());

            var commitMessage = commitMessageGenerator.GenerateForImageUpdates(new GitCommitSummary(commitParameters.CommitSummary), commitParameters.CommitDescription, updatedImages);

            Log.Info("Commiting changes");
            if (repository.CommitChanges(commitMessage))
            {
                Log.Info("Changes were commited, pushing to remote");
                repository.PushChanges(commitParameters.RequiresPr, branchName, CancellationToken.None).GetAwaiter().GetResult();
            }
            else
            {
                Log.Info("No changes were commited.");
            }
        }

        //NOTE: rootPath needs to include the subfolder
        IEnumerable<string> FindYamlFiles(string rootPath)
        {
            var yamlFileGlob = "**/*.yaml";
            return fileSystem.EnumerateFilesWithGlob(rootPath, yamlFileGlob);
        }
    }
}

#endif
