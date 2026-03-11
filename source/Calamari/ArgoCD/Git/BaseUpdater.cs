using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Patching;
using Calamari.Kubernetes.Patching.JsonPatch;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD.Git;

public interface SourceUpdater
{
    SourceUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata);
}

public abstract class BaseUpdater : SourceUpdater
{
    protected RepositoryFactory repositoryFactory;
    protected Dictionary<string, GitCredentialDto> gitCredentials;
    protected readonly ILog log;
    protected readonly ICalamariFileSystem fileSystem;
    readonly ICommitMessageGenerator commitMessageGenerator;

    protected BaseUpdater(RepositoryFactory repositoryFactory,
                          Dictionary<string, GitCredentialDto> gitCredentials,
                          ILog log,
                          ICommitMessageGenerator commitMessageGenerator,
                          ICalamariFileSystem fileSystem)
    {
        this.repositoryFactory = repositoryFactory;
        this.gitCredentials = gitCredentials;
        this.log = log;
        this.commitMessageGenerator = commitMessageGenerator;
        this.fileSystem = fileSystem;
    }

    protected RepositoryWrapper CreateRepository(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        var source = sourceWithMetadata.Source;
        var gitCredential = gitCredentials.GetValueOrDefault(source.OriginalRepoUrl);
        if (gitCredential == null)
        {
            log.Info($"No Git credentials found for: '{source.OriginalRepoUrl}', will attempt to clone repository anonymously.");
        }

        var gitConnection = new GitConnection(gitCredential?.Username, gitCredential?.Password, source.CloneSafeRepoUrl, GitReference.CreateFromString(source.TargetRevision));
        return repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
    }

    protected (HashSet<string>, HashSet<string>, List<FilePathContent>) Update(string rootPath, IReadOnlyCollection<ContainerImageReferenceAndHelmReference> imagesToUpdate, HashSet<string> filesToUpdate, Func<string, IContainerImageReplacer> imageReplacerFactory)
    {
        var updatedFiles = new HashSet<string>();
        var updatedImages = new HashSet<string>();
        var jsonPatches = new List<FilePathContent>();
        foreach (var file in filesToUpdate)
        {
            var relativePath = Path.GetRelativePath(rootPath, file);
            log.Verbose($"Processing file {relativePath}.");
            var content = fileSystem.ReadFile(file);

            var imageReplacer = imageReplacerFactory(content);
            var imageReplacementResult = imageReplacer.UpdateImages(imagesToUpdate);

            if (imageReplacementResult.UpdatedImageReferences.Count > 0)
            {
                // Replace \ with / so that Calamari running on windows doesn't cause issues when we send back to server
                jsonPatches.Add(new(relativePath.Replace('\\', '/'), UpdaterHelpers.Serialize(UpdaterHelpers.CreateJsonPatch(content, imageReplacementResult.UpdatedContents))));
                fileSystem.OverwriteFile(file, imageReplacementResult.UpdatedContents);
                updatedImages.UnionWith(imageReplacementResult.UpdatedImageReferences);
                updatedFiles.Add(relativePath);
                log.Verbose($"Updating file {relativePath} with new image references.");
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

        return (updatedFiles, updatedImages, jsonPatches);
    }

    protected PushResult? PushToRemote(
        RepositoryWrapper repository,
        GitReference branchName,
        GitCommitParameters commitParameters,
        HashSet<string> updatedFiles,
        HashSet<string> updatedImages)
    {
        log.Info("Staging files in repository");
        repository.StageFiles(updatedFiles.ToArray());

        var commitDescription = commitMessageGenerator.GenerateDescription(updatedImages, commitParameters.Description);

        log.Info("Commiting changes");
        if (!repository.CommitChanges(commitParameters.Summary, commitDescription))
            return null;

        log.Verbose("Pushing to remote");
        return repository.PushChanges(commitParameters.RequiresPr,
                                      commitParameters.Summary,
                                      commitDescription,
                                      branchName,
                                      CancellationToken.None)
                         .GetAwaiter()
                         .GetResult();
    }

    public abstract SourceUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata);
}