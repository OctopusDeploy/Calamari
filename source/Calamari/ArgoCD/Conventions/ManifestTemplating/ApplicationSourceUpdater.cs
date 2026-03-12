using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions.ManifestTemplating;

public class ApplicationSourceUpdater
{
    readonly Application applicationFromYaml;
    readonly DeploymentScope deploymentScope;
    readonly AuthenticatingRepositoryFactory repositoryFactory;
    readonly ArgoCommitToGitConfig deploymentConfig;
    readonly IPackageRelativeFile[] packageFiles;
    readonly ArgoCDGatewayDto gateway;
    readonly ILog log;
    readonly ICalamariFileSystem fileSystem;
    readonly ArgoCDOutputVariablesWriter outputVariablesWriter;

    public ApplicationSourceUpdater(Application applicationFromYaml,
                                    ArgoCDGatewayDto gateway,
                                    DeploymentScope deploymentScope,
                                    AuthenticatingRepositoryFactory repositoryFactory,
                                    ArgoCommitToGitConfig deploymentConfig,
                                    IPackageRelativeFile[] packageFiles,
                                    ILog log,
                                    ICalamariFileSystem fileSystem,
                                    ArgoCDOutputVariablesWriter outputVariablesWriter)
    {
        this.applicationFromYaml = applicationFromYaml;
        this.deploymentScope = deploymentScope;
        this.repositoryFactory = repositoryFactory;
        this.deploymentConfig = deploymentConfig;
        this.packageFiles = packageFiles;
        this.gateway = gateway;
        this.log = log;
        this.fileSystem = fileSystem;
        this.outputVariablesWriter = outputVariablesWriter;
    }

    public ManifestUpdateResult ProcessSource(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        var applicationSource = sourceWithMetadata.Source;
        var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(applicationSource.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, applicationFromYaml.Spec.Sources.Count > 1);

        log.LogApplicationSourceScopeStatus(annotatedScope, applicationSource.Name.ToApplicationSourceName(), deploymentScope);

        if (!deploymentScope.Matches(annotatedScope))
            return new ManifestUpdateResult(false, string.Empty, []);

        log.Info($"Writing files to repository '{applicationSource.OriginalRepoUrl}' for '{applicationFromYaml.Metadata.Name}'");

        if (!TryCalculateOutputPath(applicationSource, out var outputPath))
        {
            return new ManifestUpdateResult(false, string.Empty, []);
        }

        using var repository = repositoryFactory.CloneRepository(applicationSource.OriginalRepoUrl, applicationSource.TargetRevision);
        log.VerboseFormat("Copying files into '{0}'", outputPath);

        if (deploymentConfig.PurgeOutputDirectory)
        {
            repository.RecursivelyStageFilesForRemoval(outputPath);
        }

        var filesToCopy = packageFiles.Select(f => new FileCopySpecification(f, repository.WorkingDirectory, outputPath)).ToList();
        CopyFiles(filesToCopy);

        var fileHashes = filesToCopy.Select(f => new FilePathContent(
                                                                     // Replace \ with / so that Calamari running on windows doesn't cause issues when we send back to server
                                                                     f.DestinationRelativePath.Replace('\\', '/'),
                                                                     HashCalculator.Hash(f.DestinationAbsolutePath)))
                                    .ToList();

        var pushResult = PushToRemote(repository, GitReference.CreateFromString(applicationSource.TargetRevision), filesToCopy.Select(ftc => ftc.DestinationRelativePath).ToArray());
        if (pushResult is not null)
        {
            outputVariablesWriter.WritePushResultOutput(gateway.Name,
                                                        applicationFromYaml.Metadata.Name,
                                                        sourceWithMetadata.Index,
                                                        pushResult);

            return new ManifestUpdateResult(true, pushResult.CommitSha, fileHashes);
        }
        
        log.Info("No changes were commited");
        return new ManifestUpdateResult(false, string.Empty, []);
    }

    bool TryCalculateOutputPath(ApplicationSource sourceToUpdate, out string outputPath)
    {
        outputPath = "";
        var sourceIdentity = string.IsNullOrEmpty(sourceToUpdate.Name) ? sourceToUpdate.OriginalRepoUrl : sourceToUpdate.Name;
        if (sourceToUpdate.Ref != null)
        {
            if (sourceToUpdate.Path != null)
            {
                log.WarnFormat("Unable to update ref source '{0}' as a path has been explicitly specified.", sourceIdentity);
                log.Warn("Please split the source into separate sources and update annotations.");
                return false;
            }

            return true;
        }

        if (sourceToUpdate.Path == null)
        {
            log.WarnFormat("Unable to update source '{0}' as a path has not been specified.", sourceIdentity);
            return false;
        }

        outputPath = sourceToUpdate.Path;
        return true;
    }

    void CopyFiles(IEnumerable<IFileCopySpecification> repositoryFiles)
    {
        foreach (var file in repositoryFiles)
        {
            log.VerboseFormat($"Copying '{file.SourceAbsolutePath}' to '{file.DestinationAbsolutePath}'");
            EnsureParentDirectoryExists(file.DestinationAbsolutePath);
            fileSystem.CopyFile(file.SourceAbsolutePath, file.DestinationAbsolutePath);
        }
    }
    
    void EnsureParentDirectoryExists(string filePath)
    {
        var destinationDirectory = Path.GetDirectoryName(filePath);
        if (destinationDirectory != null)
        {
            fileSystem.CreateDirectory(destinationDirectory);
        }
    }

    //this _nearly_ duplicated from RepositoryAdapter
    PushResult? PushToRemote(
        RepositoryWrapper repository,
        GitReference branchName,
        string[] filesToPersist)
    {
        log.Info("Staging files in repository");
        repository.StageFiles(filesToPersist.ToArray());

        log.Info("Commiting changes");
        if (!repository.CommitChanges(deploymentConfig.CommitParameters.Summary, deploymentConfig.CommitParameters.Description))
            return null;

        log.Verbose("Pushing to remote");
        return repository.PushChanges(deploymentConfig.CommitParameters.RequiresPr,
                                      deploymentConfig.CommitParameters.Summary,
                                      deploymentConfig.CommitParameters.Description,
                                      branchName,
                                      CancellationToken.None)
                         .GetAwaiter()
                         .GetResult();
    }

    public record ManifestUpdateResult(bool Updated, string CommitSha, List<FilePathContent> ReplacedFiles);
}