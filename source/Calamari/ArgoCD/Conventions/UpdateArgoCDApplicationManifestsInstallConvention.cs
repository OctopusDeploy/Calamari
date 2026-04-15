#nullable enable
using System;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions.ManifestTemplating;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Time;

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
        readonly IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver;
        readonly IClock clock;
        readonly IArgoCDFilesUpdatedReporter reporter;
        readonly ArgoCDOutputVariablesWriter outputVariablesWriter;

        public UpdateArgoCDApplicationManifestsInstallConvention(
            ICalamariFileSystem fileSystem,
            string packageSubfolder,
            ILog log,
            DeploymentConfigFactory deploymentConfigFactory,
            ICustomPropertiesLoader customPropertiesLoader,
            IArgoCDApplicationManifestParser argoCdApplicationManifestParser,
            IArgoCDManifestsFileMatcher argoCDManifestsFileMatcher,
            IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver,
            IClock clock,
            IArgoCDFilesUpdatedReporter reporter,
            ArgoCDOutputVariablesWriter outputVariablesWriter)
        {
            this.fileSystem = fileSystem;
            this.log = log;
            this.deploymentConfigFactory = deploymentConfigFactory;
            this.customPropertiesLoader = customPropertiesLoader;
            this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
            this.argoCDManifestsFileMatcher = argoCDManifestsFileMatcher;
            this.gitVendorPullRequestClientResolver = gitVendorPullRequestClientResolver;
            this.clock = clock;
            this.outputVariablesWriter = outputVariablesWriter;
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
                gitVendorPullRequestClientResolver,
                clock);

            var argoProperties = customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>();

            var gitCredentials = argoProperties.Credentials.ToDictionary(c => c.Url);
            var authenticatingRepositoryFactory = new AuthenticatingRepositoryFactory(gitCredentials, repositoryFactory, log);
            var deploymentScope = deployment.Variables.GetDeploymentScope();

            log.LogApplicationCounts(deploymentScope, argoProperties.Applications);

            var applicationUpdater = new ApplicationUpdater(authenticatingRepositoryFactory,
                                                            deploymentScope,
                                                            deploymentConfig,
                                                            log,
                                                            fileSystem,
                                                            argoCdApplicationManifestParser,
                                                            outputVariablesWriter,
                                                            packageFiles,
                                                            new ImageTagUpdateCommitMessageGenerator(deploymentConfig.CommitParameters.Description));

            var applicationResults = argoProperties.Applications
                                                   .Select(application =>
                                                           {
                                                               var gateway = argoProperties.Gateways.Single(g => g.Id == application.GatewayId);
                                                               return applicationUpdater.ProcessApplication(application, gateway);
                                                           })
                                                   .ToList();

            reporter.ReportFilesUpdated(applicationResults);

            var gitReposUpdated = applicationResults.SelectMany(r => r.GitReposUpdated).ToHashSet();
            var totalApplicationsWithSourceCounts = applicationResults.Select(r => (r.ApplicationName, r.TotalSourceCount, r.MatchingSourceCount)).ToList();
            var updatedApplicationsWithSources = applicationResults.Where(r => r.UpdatedSourceCount > 0).Select(r => (r.ApplicationName, r.UpdatedSourceCount)).ToList();

            var gatewayIds = argoProperties.Applications.Select(a => a.GatewayId).ToHashSet();
            outputVariablesWriter.WriteManifestUpdateOutput(gatewayIds,
                gitReposUpdated,
                totalApplicationsWithSourceCounts,
                updatedApplicationsWithSources
            );
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
    }
}