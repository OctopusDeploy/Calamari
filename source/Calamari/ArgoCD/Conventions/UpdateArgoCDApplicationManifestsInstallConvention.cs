#nullable enable
using System;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Conventions.ManifestTemplating;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Time;
using Octopus.Calamari.Contracts.ArgoCD;

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

            var authenticatingRepositoryFactory = new AuthenticatingRepositoryFactory(argoProperties.Credentials, repositoryFactory, log);
            var deploymentScope = deployment.Variables.GetDeploymentScope();

            log.LogApplicationCounts(deploymentScope, argoProperties.Applications);

            var applicationUpdater = new ApplicationUpdater(deploymentScope,
                                                            deploymentConfig,
                                                            log,
                                                            fileSystem,
                                                            argoCdApplicationManifestParser,
                                                            outputVariablesWriter,
                                                            packageFiles);

            // Phase 1: plan every application's in-scope sources.
            var plans = argoProperties.Applications
                                      .Select(application =>
                                              {
                                                  var gateway = argoProperties.Gateways.Single(g => g.Id == application.GatewayId);
                                                  return applicationUpdater.Plan(application, gateway);
                                              })
                                      .ToList();

            // Phase 2: process all sources, grouped so each repository is cloned once and each branch checked out once.
            var commitMessageGenerator = new UserDefinedCommitMessageGenerator(deploymentConfig.CommitParameters.Description);
            var processor = new GroupedRepositoryProcessor(authenticatingRepositoryFactory, deploymentConfig.CommitParameters, commitMessageGenerator);

            var updates = plans.SelectMany(p => p.Sources.Select(s => s.Update)).ToList();
            var results = processor.Process(updates);
            var resultsByUpdate = updates.Zip(results, (update, result) => (update, result)).ToDictionary(x => x.update, x => x.result);

            // Phase 3: assemble per-application results (also writes per-source output variables).
            var applicationResults = plans.Select(p => applicationUpdater.AssembleResult(p, resultsByUpdate)).ToList();

            reporter.ReportFilesUpdated(deploymentConfig.CommitParameters, applicationResults);

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