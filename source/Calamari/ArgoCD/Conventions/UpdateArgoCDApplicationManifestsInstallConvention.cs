#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.GitVendorApiAdapters;
using Calamari.ArgoCD.Models;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Time;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDApplicationManifestsInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly string packageSubfolder;
        readonly DeploymentConfigFactory deploymentConfigFactory;
        readonly ICustomPropertiesLoader customPropertiesLoader;
        readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser;
        readonly IArgoCDManifestsFileMatcher argoCDManifestsFileMatcher;
        readonly IGitVendorAgnosticApiAdapterFactory gitVendorAgnosticApiAdapterFactory;
        readonly IClock clock;
        readonly IArgoCDDeploymentReporter reporter;

        public UpdateArgoCDApplicationManifestsInstallConvention(ICalamariFileSystem fileSystem,
                                                                 string packageSubfolder,
                                                                 ILog log,
                                                                 DeploymentConfigFactory deploymentConfigFactory,
                                                                 ICustomPropertiesLoader customPropertiesLoader,
                                                                 IArgoCDApplicationManifestParser argoCdApplicationManifestParser,
                                                                 IArgoCDManifestsFileMatcher argoCDManifestsFileMatcher,
                                                                 IGitVendorAgnosticApiAdapterFactory gitVendorAgnosticApiAdapterFactory,
                                                                 IClock clock,
                                                                 IArgoCDDeploymentReporter reporter)
        {
            this.fileSystem = fileSystem;
            this.log = log;
            this.deploymentConfigFactory = deploymentConfigFactory;
            this.customPropertiesLoader = customPropertiesLoader;
            this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
            this.argoCDManifestsFileMatcher = argoCDManifestsFileMatcher;
            this.gitVendorAgnosticApiAdapterFactory = gitVendorAgnosticApiAdapterFactory;
            this.clock = clock;
            this.packageSubfolder = packageSubfolder;
            this.reporter = reporter;
        }

        public void Install(RunningDeployment deployment)
        {
            log.Verbose("Executing Update Argo CD Application manifests operation");
            var deploymentConfig = deploymentConfigFactory.CreateCommitToGitConfig(deployment);
            var packageFiles = GetReferencedPackageFiles(deploymentConfig);

            var repositoryFactory = new RepositoryFactory(log,
                                                          fileSystem,
                                                          deployment.CurrentDirectory,
                                                          gitVendorAgnosticApiAdapterFactory,
                                                          clock);

            var argoProperties = customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>();

            var gitCredentials = argoProperties.Credentials.ToDictionary(c => c.Url);
            var deploymentScope = deployment.Variables.GetDeploymentScope();

            log.LogApplicationCounts(deploymentScope, argoProperties.Applications);

            var applicationResults = argoProperties.Applications
                                                   .Select(application => ProcessApplication(application,
                                                                                             deploymentScope,
                                                                                             gitCredentials,
                                                                                             repositoryFactory,
                                                                                             deploymentConfig,
                                                                                             packageFiles))
                                                   .ToList();

            reporter.ReportDeployments(applicationResults);

            var gitReposUpdated = applicationResults.SelectMany(r => r.GitReposUpdated).ToHashSet();
            var totalApplicationsWithSourceCounts = applicationResults.Select(r => (r.ApplicationName, r.TotalSourceCount, r.MatchingSourceCount)).ToList();
            var updatedApplicationsWithSources = applicationResults.Where(r => r.UpdatedSourceCount > 0).Select(r => (r.ApplicationName, r.UpdatedSourceCount)).ToList();

            var gatewayIds = argoProperties.Applications.Select(a => a.GatewayId).ToHashSet();
            var outputWriter = new ArgoCDOutputVariablesWriter(log);
            outputWriter.WriteManifestUpdateOutput(gatewayIds,
                                                   gitReposUpdated,
                                                   totalApplicationsWithSourceCounts,
                                                   updatedApplicationsWithSources
                                                  );
        }

        ProcessApplicationResult ProcessApplication(ArgoCDApplicationDto application,
                                                    DeploymentScope deploymentScope,
                                                    Dictionary<string, GitCredentialDto> gitCredentials,
                                                    RepositoryFactory repositoryFactory,
                                                    ArgoCommitToGitConfig deploymentConfig,
                                                    IPackageRelativeFile[] packageFiles)
        {
            log.InfoFormat("Processing application {0}", application.Name);
            var applicationFromYaml = argoCdApplicationManifestParser.ParseManifest(application.Manifest);
            var containsMultipleSources = applicationFromYaml.Spec.Sources.Count > 1;
            var applicationName = applicationFromYaml.Metadata.Name;

            LogWarningIfUpdatingMultipleSources(applicationFromYaml.Spec.Sources,
                                                applicationFromYaml.Metadata.Annotations,
                                                containsMultipleSources,
                                                deploymentScope);
            var validationResult = ApplicationValidator.Validate(applicationFromYaml);
            validationResult.Action(log);

            var updatedSourcesResults = applicationFromYaml
                                        .GetSourcesWithMetadata()
                                        .Select(applicationSource => new
                                        {
                                            UpdateResult = ProcessSource(deploymentScope,
                                                                         gitCredentials,
                                                                         repositoryFactory,
                                                                         deploymentConfig,
                                                                         packageFiles,
                                                                         applicationSource,
                                                                         applicationFromYaml,
                                                                         containsMultipleSources,
                                                                         applicationName),
                                            applicationSource
                                        })
                                        .Where(u => u.UpdateResult.Updated)
                                        .ToList();

            //if we have links, use that to generate a link, otherwise just put the name there
            var instanceLinks = application.InstanceWebUiUrl != null ? new ArgoCDInstanceLinks(application.InstanceWebUiUrl) : null;
            var linkifiedAppName = instanceLinks != null
                ? log.FormatLink(instanceLinks.ApplicationDetails(applicationName, applicationFromYaml.Metadata.Namespace), applicationName)
                : applicationName;

            var message = updatedSourcesResults.Any()
                ? "Updated Application {0}"
                : "Nothing to update for Application {0}";

            log.InfoFormat(message, linkifiedAppName);

            return new ProcessApplicationResult(application.GatewayId, applicationName.ToApplicationName())
            {
                TotalSourceCount = applicationFromYaml.Spec.Sources.Count,
                MatchingSourceCount = applicationFromYaml.Spec.Sources.Count(s => deploymentScope.Matches(ScopingAnnotationReader.GetScopeForApplicationSource(s.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources))),
                GitReposUpdated = updatedSourcesResults.Select(r => r.applicationSource.Source.OriginalRepoUrl).ToHashSet(),
                UpdatedSourceDetails = updatedSourcesResults.Select(r => new UpdatedSourceDetail(r.UpdateResult.CommitSha, r.applicationSource.Index, [], [])).ToList()
            };
        }

        ManifestUpdateResult ProcessSource(DeploymentScope deploymentScope,
                                           Dictionary<string, GitCredentialDto> gitCredentials,
                                           RepositoryFactory repositoryFactory,
                                           ArgoCommitToGitConfig deploymentConfig,
                                           IPackageRelativeFile[] packageFiles,
                                           ApplicationSourceWithMetadata sourceWithMetadata,
                                           Application applicationFromYaml,
                                           bool containsMultipleSources,
                                           string applicationName)
        {
            var applicationSource = sourceWithMetadata.Source;
            var annotatedScope = ScopingAnnotationReader.GetScopeForApplicationSource(applicationSource.Name.ToApplicationSourceName(), applicationFromYaml.Metadata.Annotations, containsMultipleSources);

            log.LogApplicationSourceScopeStatus(annotatedScope, applicationSource.Name.ToApplicationSourceName(), deploymentScope);

            if (!deploymentScope.Matches(annotatedScope))
                return new ManifestUpdateResult(false, string.Empty);

            log.Info($"Writing files to repository '{applicationSource.OriginalRepoUrl}' for '{applicationName}'");

            if (!TryCalculateOutputPath(applicationSource, out var outputPath))
            {
                return new ManifestUpdateResult(false, string.Empty);
            }

            var gitCredential = gitCredentials.GetValueOrDefault(applicationSource.OriginalRepoUrl);
            if (gitCredential == null)
            {
                log.Info($"No Git credentials found for: '{applicationSource.OriginalRepoUrl}', will attempt to clone repository anonymously.");
            }

            var targetBranch = GitReference.CreateFromString(applicationSource.TargetRevision);
            var gitConnection = new GitConnection(gitCredential?.Username, gitCredential?.Password, applicationSource.CloneSafeRepoUrl, targetBranch);

            using var repository = repositoryFactory.CloneRepository(UniqueRepoNameGenerator.Generate(), gitConnection);
            log.VerboseFormat("Copying files into '{0}'", outputPath);

            if (deploymentConfig.PurgeOutputDirectory)
            {
                repository.RecursivelyStageFilesForRemoval(outputPath);
            }

            var filesToCopy = packageFiles.Select(f => new FileCopySpecification(f, repository.WorkingDirectory, outputPath)).ToList();
            CopyFiles(filesToCopy);

            log.Info("Staging files in repository");
            repository.StageFiles(filesToCopy.Select(fcs => fcs.DestinationRelativePath).ToArray());

            log.Info("Commiting changes");
            if (repository.CommitChanges(deploymentConfig.CommitParameters.Summary, deploymentConfig.CommitParameters.Description))
            {
                var commitSha = repository.GetCommitSha();

                log.Info("Changes were commited, pushing to remote");
                repository.PushChanges(deploymentConfig.CommitParameters.RequiresPr,
                                       deploymentConfig.CommitParameters.Summary,
                                       deploymentConfig.CommitParameters.Description,
                                       targetBranch,
                                       CancellationToken.None)
                          .GetAwaiter()
                          .GetResult();

                return new ManifestUpdateResult(true, commitSha);
            }

            log.Info("No changes were commited");

            return new ManifestUpdateResult(false, string.Empty);
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

        //TODO(tmm): should we be removing this warning now
        void LogWarningIfUpdatingMultipleSources(List<ApplicationSource> sourcesToInspect,
                                                 Dictionary<string, string> applicationAnnotations,
                                                 bool containsMultipleSources,
                                                 DeploymentScope deploymentScope)
        {
            if (sourcesToInspect.Count > 1)
            {
                var sourcesWithScopes = sourcesToInspect.Select(s => (s, ScopingAnnotationReader.GetScopeForApplicationSource(s.Name.ToApplicationSourceName(), applicationAnnotations, containsMultipleSources))).ToList();
                var sourcesWithMatchingScopes = sourcesWithScopes.Where(s => deploymentScope.Matches(s.Item2)).ToList();

                if (sourcesWithMatchingScopes.Count > 1)
                {
                    log.Warn($"Multiple sources are associated with this deployment, they will all be updated with the same contents: {string.Join(", ", sourcesWithMatchingScopes.Select(s => s.s.Name))}");
                }
            }
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

        IPackageRelativeFile[] GetReferencedPackageFiles(ArgoCommitToGitConfig config)
        {
            log.Info($"Selecting files from package using '{config.InputSubPath}'");
            var filesToApply = SelectFiles(Path.Combine(config.WorkingDirectory, packageSubfolder), config);
            log.Info($"Found {filesToApply.Length} files to apply");
            return filesToApply;
        }

        IPackageRelativeFile[] SelectFiles(string pathToExtractedPackageFiles, ArgoCommitToGitConfig config)
            => argoCDManifestsFileMatcher.FindMatchingPackageFiles(pathToExtractedPackageFiles, config.InputSubPath);

        record ManifestUpdateResult(bool Updated, string CommitSha);
    }
}
