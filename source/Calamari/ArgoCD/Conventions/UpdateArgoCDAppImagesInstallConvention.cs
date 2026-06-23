#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
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
    public class UpdateArgoCDAppImagesInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly DeploymentConfigFactory deploymentConfigFactory;
        readonly ICustomPropertiesLoader customPropertiesLoader;
        readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser;
        readonly IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver;
        readonly IClock clock;
        readonly IArgoCDFilesUpdatedReporter reporter;
        readonly ArgoCDOutputVariablesWriter outputVariablesWriter;

        public UpdateArgoCDAppImagesInstallConvention(
            ILog log,
            ICalamariFileSystem fileSystem,
            DeploymentConfigFactory deploymentConfigFactory,
            ICustomPropertiesLoader customPropertiesLoader,
            IArgoCDApplicationManifestParser argoCdApplicationManifestParser,
            IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver,
            IClock clock,
            IArgoCDFilesUpdatedReporter reporter,
            ArgoCDOutputVariablesWriter outputVariablesWriter)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.deploymentConfigFactory = deploymentConfigFactory;
            this.customPropertiesLoader = customPropertiesLoader;
            this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
            this.gitVendorPullRequestClientResolver = gitVendorPullRequestClientResolver;
            this.clock = clock;
            this.reporter = reporter;
            this.outputVariablesWriter = outputVariablesWriter;
        }

        public void Install(RunningDeployment deployment)
        {
            log.Verbose("Executing Update Argo CD Application Images");
            var deploymentConfig = deploymentConfigFactory.CreateUpdateImageConfig(deployment);

            var repositoryFactory = new RepositoryFactory(log,
                                                          fileSystem,
                                                          deployment.CurrentDirectory,
                                                          gitVendorPullRequestClientResolver,
                                                          clock);

            var argoProperties = customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>();

            var authenticatingRepositoryFactory = new AuthenticatingRepositoryFactory(argoProperties.Credentials, repositoryFactory, log);
            var deploymentScope = deployment.Variables.GetDeploymentScope();

            log.LogApplicationCounts(deploymentScope, argoProperties.Applications);

            var appUpdater = new ApplicationUpdater(deploymentScope,
                                                    deploymentConfig,
                                                    log,
                                                    fileSystem,
                                                    argoCdApplicationManifestParser,
                                                    outputVariablesWriter);

            // Phase 1: plan every application's in-scope sources.
            var plans = argoProperties.Applications
                                      .Select(application =>
                                              {
                                                  var gateway = argoProperties.Gateways.Single(g => g.Id == application.GatewayId);
                                                  return appUpdater.Plan(application, gateway);
                                              })
                                      .ToList();

            // Phase 2: process all sources, grouped so each repository is cloned once and each branch checked out once.
            var commitMessageGenerator = new ImageTagUpdateCommitMessageGenerator(deploymentConfig.CommitParameters.Description);
            var processor = new GroupedRepositoryProcessor(authenticatingRepositoryFactory, deploymentConfig.CommitParameters, commitMessageGenerator);

            var updates = plans.SelectMany(p => p.Sources.Select(s => s.Update)).ToList();
            var results = processor.Process(updates);
            var resultsByUpdate = updates.Zip(results, (update, result) => (update, result)).ToDictionary(x => x.update, x => x.result);

            // Phase 3: assemble per-application results (also writes per-source output variables).
            var applicationResults = plans.Select(p => appUpdater.AssembleResult(p, resultsByUpdate)).ToList();

            //if we are creating a pull request, we don't want to report files updated (as this will be passed down as output variables _with_ the PR info)

                reporter.ReportFilesUpdated(deploymentConfig.CommitParameters, applicationResults);

            var totalApplicationsWithSourceCounts = applicationResults.Select(r => (r.ApplicationName, r.TotalSourceCount, r.MatchingSourceCount)).ToList();
            var updatedApplications = applicationResults.Where(r => r.Updated).ToList();
            var updatedApplicationsWithSources = updatedApplications.Select(r => (r.ApplicationName, r.UpdatedSourceCount)).ToList();
            var gitReposUpdated = updatedApplications.SelectMany(r => r.GitReposUpdated).ToHashSet();
            var newImagesWritten = updatedApplications.SelectMany(r => r.UpdatedImages).ToHashSet();

            var gatewayIds = argoProperties.Applications.Select(a => a.GatewayId).ToHashSet();
            outputVariablesWriter.WriteImageUpdateOutput(gatewayIds,
                                                         gitReposUpdated,
                                                         totalApplicationsWithSourceCounts,
                                                         updatedApplicationsWithSources,
                                                         newImagesWritten.Count
                                                        );
        }
    }
}